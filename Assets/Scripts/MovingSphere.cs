using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{

    [SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)]
	float maxAcceleration = 10f;

	[SerializeField, Range(0f, 100f)]
	float maxAirAcceleration = 1f; // 在空中的加速度, 直白的说就是跳到空中后, 玩家控制运动的能力

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;

	[SerializeField, Range(0f, 90f)]
	float maxGroundAngle = 25f; // 地面倾角; 值越小, 越平的表面才会被判定为地面

	[SerializeField, Range(0, 90)]
	float maxStairsAngle = 50f; // 楼梯倾角

	[SerializeField, Range(0f, 100f)]
	float maxSnapSpeed = 100f;

	[SerializeField, Min(0f)]
	float probeDistance = 1f; // 当物体腾空时, 只 snap 一段距离内的 ground; 

	[SerializeField]
	LayerMask probeMask = -1;

	[SerializeField]
	LayerMask stairsMask = -1;

	[SerializeField]
	Transform playerInputSpace = default; // 绑定 主相机

	Rigidbody body, 				// 本小球
			connectedBody, 			// 本帧的 connect plane
			previousConnectedBody;	// 上帧的 connect plane

    Vector3 velocity, 			// 本帧实际执行的 速度值
			desiredVelocity, 	// 
			connectionVelocity;	// 
	
	Vector3 contactNormal, steepNormal;

	// 重力坐标系 由一个全局系统统一提供:
	Vector3 upAxis, 	// 在当前 自定义的重力系统下, 的 up 方向;
			rightAxis, 	// 在当前 自定义的重力系统下, 的 right 方向;
			forwardAxis;// 在当前 自定义的重力系统下, 的 forward 方向;

	Vector3 connectionWorldPosition, connectionLocalPosition;

	bool desiredJump;

	int jumpPhase;
	int groundContactCount, steepContactCount;

	bool OnGround => groundContactCount > 0;
	bool OnSteep => steepContactCount > 0;


	float minGroundDotProduct;
	float minStairsDotProduct;
	int stepsSinceLastGrounded;	// 只要接触 ground, 此值始终为 0, 一旦离开 ground, 此值开始无限累加 (fixed update)
	int stepsSinceLastJump;		// 每触发一次跳跃, 此值被清零, 然后无限累加 (fixed update)
	


	void Awake () 
	{
		body = GetComponent<Rigidbody>();
		body.useGravity = false; // 改用自己实现的 重力系统
		OnValidate();

		// 只有在 FixedUpdate() 调用时, 物理系统才会更新 pos, 所以此值过高时, 物体运动就会一卡一卡的;
		Time.fixedDeltaTime = 0.01f;
		//Time.timeScale = 0.8f; // 慢速播放
	}
 
    

    void Update () 
    {
		// ----------------- 接收 用户输入 -------------------
		Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

		if (playerInputSpace) 
		{
			// 主观视角的运动控制; 使用 相机的 right 和 forward;
			rightAxis   = ProjectDirectionOnPlane(playerInputSpace.right,   upAxis);
			forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
		}
		else 
		{
			// 客观视角的运动控制, 使用 ws 中的 right 和 forward;
			rightAxis   = ProjectDirectionOnPlane(Vector3.right,   upAxis);
			forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
		}

		desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

		desiredJump |= Input.GetButtonDown("Jump"); // remains true

		GetComponent<Renderer>().material.SetColor(
			"_BaseColor", OnGround ? Color.black : Color.white
		);
	}


	void FixedUpdate () 
	{
		Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis); // 每帧都更新
		UpdateState();
		AdjustVelocity();

		if (desiredJump) 
		{
			desiredJump = false;
			Jump( gravity );
		}

		// 施加 本帧的自定义重力 作用;
		velocity += gravity * Time.deltaTime;

		body.velocity = velocity;
		ClearState();
	}


	void OnCollisionEnter (Collision collision) 
	{
		EvaluateCollision(collision);
	}


	void OnCollisionStay (Collision collision) 
	{
		EvaluateCollision(collision);
	}


	void EvaluateCollision (Collision collision) 
	{
		float minDot = GetMinDot(collision.gameObject.layer);// 要么是 ground, 要么是 stairs
		for (int i = 0; i < collision.contactCount; i++) 
		{
			Vector3 normal = collision.GetContact(i).normal;
			float upDot = Vector3.Dot(upAxis, normal);
			// 确定此平面为 ground 或 stair
			if (upDot >= minDot) 
			{
				groundContactCount += 1;
				contactNormal += normal;
				connectedBody = collision.rigidbody;
			}
			// 只要这个表面不是绝对向下的, 则都算是 steep 表面;
			else if (upDot > -0.01f) 
			{
				steepContactCount += 1;
				steepNormal += normal;

				// 只有在没找到 ground 时, 才把 steep 当作 connectedBody
				if (groundContactCount == 0) 
				{
					connectedBody = collision.rigidbody;
				}
			}

		}
	}


	void OnValidate () 
	{
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
	}


	// FixedUpdate() 每帧开始时被调用;
	void UpdateState () 
	{
		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity; // 这样就不会卡在墙边了;

		// -1- 本帧 正在接触了 ground;
		// -2- 本帧腾空了, 但被判定为: 需要 snap to ground;
		// -3- 以上都不是时, 有可能被 卡在缝隙里了, 此时会将缝隙替换为一个 虚拟平面
		if ( OnGround || SnapToGround() || CheckSteepContacts() ) 
		{
			stepsSinceLastGrounded = 0;
			if (stepsSinceLastJump > 1) // 新的跳跃之后的 第2帧, (观察 stepsSinceLastJump 的累加位置)
			{
				jumpPhase = 0;
			}
			if (groundContactCount > 1) 
			{
				contactNormal.Normalize();
			}
		}
		else 
		{
			contactNormal = upAxis; // 服务于 空中跳跃
		}

		// 如果本帧检测到 connectedBody
		if (connectedBody) 
		{
			
			if (connectedBody.isKinematic || connectedBody.mass >= body.mass) 
			{
				UpdateConnectionState();
			}
		}

		Log_In_FixedUpdate(); // tpr
	}

	void UpdateConnectionState () 
	{

		// 只有当 本帧 和 上帧 接触同一个下方接触面时
		if (connectedBody == previousConnectedBody) 
		{
			// 本帧 接触点的 运动向量
			Vector3 connectionMovement =
				connectedBody.transform.TransformPoint(connectionLocalPosition)
				- connectionWorldPosition;
			connectionVelocity = connectionMovement / Time.deltaTime;
		}
		// 否则, 若接触的不是同一个 connext plane, 则让 connectionVelocity 维持为 0;

		// 直接使用 小球的 posws 当作 接触点pos; (一种简化)
		connectionWorldPosition = body.position;
		// 转换出 上述点 在 connect 平面的 os 坐标系里的表达'
		connectionLocalPosition = connectedBody.transform.InverseTransformPoint(
			connectionWorldPosition
		);
	}


	void Jump( Vector3 gravity )
	{
		Vector3 jumpDirection;
		if (OnGround) // --- 从 ground 上跳跃
		{
			jumpDirection = contactNormal;
		}
		else if (OnSteep) // --- 从 缝隙 中跳出来
		{
			jumpDirection = steepNormal;
			jumpPhase = 0;
		}
		else if ( maxAirJumps > 0 && jumpPhase <= maxAirJumps) // --- 空中 N段跳
		{
			if (jumpPhase == 0) 
			{
				jumpPhase = 1;
			}
			jumpDirection = contactNormal; // 此时此值为 up
		}
		else 
		{
			return;
		}

		
		stepsSinceLastJump = 0;
		jumpPhase += 1;
		
		float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);

		// 为支持 从墙壁上向 斜上方跳跃
		jumpDirection = (jumpDirection + upAxis).normalized;

		float alignedSpeed = Vector3.Dot(velocity, jumpDirection);

		// 防止玩家通过两次 短间隔 的二连跳 来达到非常高的 上跳速度;
		if (alignedSpeed > 0f) 
		{
			jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
		}

		velocity += jumpDirection * jumpSpeed;
		
	}


	


	// 获得 vector 沿着 normal 代表的表面 的分量; 
	Vector3 ProjectDirectionOnPlane (Vector3 direction, Vector3 normal) 
	{
		return (direction - normal * Vector3.Dot(direction, normal)).normalized;
	}


	void AdjustVelocity () 
	{
		// 沿着本帧 connect plane 的 xz 坐标系;
		Vector3 xAxis = ProjectDirectionOnPlane(rightAxis,   contactNormal);
		Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);


		// 为何是 反方向速度呢, 因为这个值 要在后续运算中 "被减掉"
        // 用这种不够直观的方式, 来叠加 "下方活动地面" 的运动速度
		Vector3 relativeVelocity = velocity - connectionVelocity;

		float currentX = Vector3.Dot(relativeVelocity, xAxis);
		float currentZ = Vector3.Dot(relativeVelocity, zAxis);

		float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + 
					zAxis * (newZ - currentZ);

	}

	void ClearState () 
	{
		groundContactCount = steepContactCount = 0;
		contactNormal = steepNormal = connectionVelocity = Vector3.zero;
		previousConnectedBody = connectedBody;
		connectedBody = null;
	}

	// 只有在本帧腾空时, 本函数才会被调用
	// ret: 是否发生了: snap to ground;
	bool SnapToGround () 
	{
		// 如果腾空超过 1 个 fixed帧, 放弃 snap;
		// stepsSinceLastJump==1  其实就是 新跳跃的 第一帧 (跳跃帧)
		// stepsSinceLastJump==2  新跳跃的第二帧;
		if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) 
		{
			return false;
		}

		// 速度过大时, 放弃 snap
		float speed = velocity.magnitude;
		if (speed > maxSnapSpeed) 
		{
			return false;
		}

		// 若球体下方没有 ground, 放弃 snap
		if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask)) 
		{
			return false;
		}

		// 检测到的 下方平面 若太陡峭, 不属于 ground, 放弃 snap
		float upDot = Vector3.Dot(upAxis, hit.normal);
		if (upDot < GetMinDot(hit.collider.gameObject.layer)) 
		{
			return false;
		}

		groundContactCount = 1;
		contactNormal = hit.normal;

		float dot = Vector3.Dot(velocity, hit.normal);
		// 只有当 velocity 方向远离 new ground 时, 才执行下方的 velocity 贴合修正操作;
		// 否则就让 velocity 继续撞向 new ground;
		if (dot > 0f) 
		{
			velocity = (velocity - hit.normal * dot).normalized * speed;
		}

		// 把这个虚拟平面 当作 connectedBody;
		connectedBody = hit.rigidbody;
		return true;
	}

	float GetMinDot (int layer) 
	{
		return (stairsMask & (1 << layer)) == 0 ?
			minGroundDotProduct : minStairsDotProduct;
	}


	// 若能将 steep 接触 转换为 与虚拟平面的接触, 则返回 true;
	// 此时, 允许 球体在这个 虚拟平面上侧向滑动
	bool CheckSteepContacts () 
	{
		if (steepContactCount > 1) 
		{
			steepNormal.Normalize();
			float upDot = Vector3.Dot(upAxis, steepNormal);
			if (upDot >= minGroundDotProduct) 
			{
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}


	void Log_In_FixedUpdate()
	{
		//Debug.Log( "stepsSinceLastJump = " + stepsSinceLastJump );
		//Debug.Log("stepsSinceLastGrounded = " + stepsSinceLastGrounded);
		//Debug.Log("jumpPhase = " + jumpPhase);
	}


}
