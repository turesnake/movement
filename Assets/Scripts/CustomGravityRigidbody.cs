using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    让各种 rigidbody 支持 自定义的重力系统
*/

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour 
{

    [SerializeField]
	bool floatToSleep = false; // true 时运行 body 进入 sleep 状态;

	Rigidbody body;
    float floatDelay;


	void Awake () 
    {
		body = GetComponent<Rigidbody>();
		body.useGravity = false;
	}


    void Update() 
    {
        // 不同的状态 涂上不同的颜色
        var mat = GetComponent<Renderer>().material;
        if( body.IsSleeping() ){
            // sleep
            mat.SetColor( "_BaseColor", Color.gray );
        }else{
            if( floatDelay < 0.0001f ){
                // awake
                mat.SetColor( "_BaseColor", Color.red );
            }else{
                // float delay
                mat.SetColor( "_BaseColor", Color.yellow );
            }
        }
        
    }


    void FixedUpdate () 
    {
        if (floatToSleep) 
        {
            if (body.IsSleeping()) 
            {
                floatDelay = 0f;
                return;
            }

            if (body.velocity.sqrMagnitude < 0.0001f) 
            {
                floatDelay += Time.deltaTime;
                if (floatDelay >= 1f) {
                    return;
                }
            }
            else 
            {
                floatDelay = 0f;
            }
        }

        // 施加自定义的 重力
		body.AddForce(
            CustomGravity.GetGravity(body.position), 
            ForceMode.Acceleration // Add a continuous acceleration to the rigidbody, ignoring its mass
        );
	}
}


