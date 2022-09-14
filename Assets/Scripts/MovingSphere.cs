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

	Rigidbody body;
    Vector3 velocity;
	Vector3 desiredVelocity;
	Vector3 contactNormal, steepNormal;
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
		OnValidate();

		// 只有在 FixedUpdate() 调用时, 物理系统才会更新 pos, 所以此值过高时, 物体运动就会一卡一卡的;
		Time.fixedDeltaTime = 0.05f;
		//Time.timeScale = 0.8f; // 慢速播放
	}
 
    

    void Update () 
    {
		// ----------------- 接收 用户输入 -------------------
		Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

		desiredJump |= Input.GetButtonDown("Jump"); // remains true

		GetComponent<Renderer>().material.SetColor(
			"_BaseColor", OnGround ? Color.black : Color.white
		);
	}


	void FixedUpdate () 
	{
		UpdateState();
		AdjustVelocity();

		if (desiredJump) 
		{
			desiredJump = false;
			Jump();
		}

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
			contactNormal = Vector3.up; // 服务于 空中跳跃
		}

		Log_In_FixedUpdate(); // tpr
	}


	void Jump() 
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
		
		float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);

		// 为支持 从墙壁上向 斜上方跳跃
		jumpDirection = (jumpDirection + Vector3.up).normalized;

		float alignedSpeed = Vector3.Dot(velocity, jumpDirection);

		// 防止玩家通过两次 短间隔 的二连跳 来达到非常高的 上跳速度;
		if (alignedSpeed > 0f) 
		{
			jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
		}

		velocity += jumpDirection * jumpSpeed;
		
	}


	void EvaluateCollision (Collision collision) 
	{
		float minDot = GetMinDot(collision.gameObject.layer);
		for (int i = 0; i < collision.contactCount; i++) 
		{
			Vector3 normal = collision.GetContact(i).normal;
			if (normal.y >= minDot) 
			{
				groundContactCount += 1;
				contactNormal += normal;
			}
			// 只要这个表面不是绝对向下的, 则都算是 steep 表面;
			else if (normal.y > -0.01f) 
			{
				steepContactCount += 1;
				steepNormal += normal;
			}

		}
	}


	// 获得 vector 沿着 ground 表面的分量; 
	Vector3 ProjectOnContactPlane (Vector3 vector) 
	{
		return vector - contactNormal * Vector3.Dot(vector, contactNormal);
	}


	void AdjustVelocity () 
	{
		Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
		Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

		float currentX = Vector3.Dot(velocity, xAxis);
		float currentZ = Vector3.Dot(velocity, zAxis);

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
		contactNormal = steepNormal = Vector3.zero;
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
		if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask)) 
		{
			return false;
		}

		// 检测到的 下方平面 若太陡峭, 不属于 ground, 放弃 snap
		if (hit.normal.y < GetMinDot(hit.collider.gameObject.layer)) 
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
			if (steepNormal.y >= minGroundDotProduct) 
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
