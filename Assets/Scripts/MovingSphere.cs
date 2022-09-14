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
	int stepsSinceLastGrounded;//腾空了几个 fixed帧;
	int stepsSinceLastJump; 
	


	void Awake () 
	{
		body = GetComponent<Rigidbody>();
		OnValidate();

		Time.fixedDeltaTime = 0.01f;
	}
 
    

    void Update () 
    {
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
		//Debug.Log("enter");
		EvaluateCollision(collision);
	}


	void OnCollisionStay (Collision collision) 
	{
		//Debug.Log("stay");
		EvaluateCollision(collision);
	}

	void OnValidate () 
	{
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
	}


	void UpdateState () 
	{
		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity; // 这样就不会卡在墙边了;

		// -1- 判定接触了 ground 时, 就不会出现 腾空
		// -2- 只有腾空后, 才需要判断是否需要 snap to ground
		// -3- 以上都不是时, 有可能被 卡在缝隙里了; 
		if ( OnGround || SnapToGround() || CheckSteepContacts() ) 
		{
			stepsSinceLastGrounded = 0;
			//jumpPhase = 0;
			if (stepsSinceLastJump > 1) 
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
	}


	void Jump() 
	{
		Vector3 jumpDirection;
		if (OnGround) 
		{
			jumpDirection = contactNormal;
		}
		else if (OnSteep) 
		{
			jumpDirection = steepNormal;
		}
		else if (jumpPhase < maxAirJumps) 
		{
			// 空中跳跃
			jumpDirection = contactNormal; // 此时此值为 up
		}
		else 
		{
			return;
		}

		//if ( OnGround || jumpPhase < maxAirJumps ) 
		//{
			stepsSinceLastJump = 0;
			jumpPhase += 1;
			
			float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);

			float alignedSpeed = Vector3.Dot(velocity, jumpDirection);

			// 防止玩家通过两次 短间隔 的二连跳 来达到非常高的 上跳速度;
			if (alignedSpeed > 0f) 
			{
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
			}

			velocity += jumpDirection * jumpSpeed;
		//}
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
		// stepsSinceLastJump 只有在 触发一次跳跃的头几帧会; 
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


}
