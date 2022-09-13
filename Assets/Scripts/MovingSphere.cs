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

	Rigidbody body;
    Vector3 velocity;
	Vector3 desiredVelocity;
	bool desiredJump;
	bool onGround;

	int jumpPhase;

	


	void Awake () 
	{
		body = GetComponent<Rigidbody>();

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

	}


	void FixedUpdate () 
	{
		//velocity = body.velocity; // 这样就不会卡在墙边了;
		UpdateState();

		float acceleration = onGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;
        
        velocity.x = Mathf.MoveTowards(velocity.x, desiredVelocity.x, maxSpeedChange);
		velocity.z = Mathf.MoveTowards(velocity.z, desiredVelocity.z, maxSpeedChange);

		if (desiredJump) 
		{
			desiredJump = false;
			Jump();
		}

		body.velocity = velocity;
		onGround = false;
	}


	void OnCollisionEnter (Collision collision) 
	{
		//Debug.Log("enter");
		//onGround = true;
		EvaluateCollision(collision);
	}


	void OnCollisionStay (Collision collision) 
	{
		//Debug.Log("stay");
		//onGround = true;
		EvaluateCollision(collision);
	}


	void UpdateState () 
	{
		velocity = body.velocity; // 这样就不会卡在墙边了;
		if (onGround) 
		{
			jumpPhase = 0;
		}
	}


	void Jump() 
	{
		if ( onGround || jumpPhase < maxAirJumps ) 
		{
			jumpPhase += 1;
			
			float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
			// 防止玩家通过两次 短间隔 的二连跳 来达到非常高的 上跳速度;
			if (velocity.y > 0f) 
			{
				jumpSpeed = Mathf.Max(jumpSpeed - velocity.y, 0f);
			}
			velocity.y += jumpSpeed;
		}
	}


	void EvaluateCollision (Collision collision) 
	{
		for (int i = 0; i < collision.contactCount; i++) 
		{
			Vector3 normal = collision.GetContact(i).normal;
			onGround |= normal.y >= 0.9f; // 找到一个 true 就算成立;
		}
	}


}
