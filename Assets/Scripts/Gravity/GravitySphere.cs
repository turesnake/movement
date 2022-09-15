using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class GravitySphere : GravitySource
{
    
    [SerializeField]
	float gravity = 9.81f;

	[SerializeField, Min(0f)]
	float outerRadius = 10f, outerFalloffRadius = 15f;

    [SerializeField, Min(0f)]
	float innerFalloffRadius = 1f, innerRadius = 5f;

    float innerFalloffFactor, outerFalloffFactor;
	

    void Awake () 
    {
		OnValidate();
	}

	void OnValidate () 
    {
        // 只是用来保证 用户设置的参数在正确的区间;

        innerFalloffRadius = Mathf.Max(innerFalloffRadius, 0f);
		innerRadius = Mathf.Max(innerRadius, innerFalloffRadius);
		outerRadius = Mathf.Max(outerRadius, innerRadius);

		outerFalloffRadius = Mathf.Max(outerFalloffRadius, outerRadius);

        innerFalloffFactor = 1f / (innerRadius - innerFalloffRadius);
        outerFalloffFactor = 1f / (outerFalloffRadius - outerRadius);
	}

	void OnDrawGizmos () 
    {
		Vector3 p = transform.position;
        if (innerFalloffRadius > 0f && innerFalloffRadius < innerRadius) 
        {
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(p, innerFalloffRadius);
		}

		Gizmos.color = Color.yellow;
        if (innerRadius > 0f && innerRadius < outerRadius) 
        {
			Gizmos.DrawWireSphere(p, innerRadius);
		}
		Gizmos.DrawWireSphere(p, outerRadius);
		if (outerFalloffRadius > outerRadius) 
        {
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(p, outerFalloffRadius);
		}
	}


    public override Vector3 GetGravity (Vector3 position) 
    {
		Vector3 vector = transform.position - position; // 指向 星球球心
		float distance = vector.magnitude;
		if (distance > outerFalloffRadius || distance < innerFalloffRadius) 
        {
			return Vector3.zero;
		}
		//float g = gravity;
		//return g * vector.normalized;

        float g = gravity / distance;

        if (distance > outerRadius) 
        {
			g *= 1f - (distance - outerRadius) * outerFalloffFactor; // 线性衰减
		}
        else if (distance < innerRadius) 
        {
			g *= 1f - (innerRadius - distance) * innerFalloffFactor;
		}

		return g * vector;
	}


}
