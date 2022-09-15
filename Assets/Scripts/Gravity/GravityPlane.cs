using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class GravityPlane : GravitySource 
{


    [SerializeField]
	float gravity = 9.81f;


    [SerializeField, Min(0f)]
	float range = 1f;

	public override Vector3 GetGravity (Vector3 position) 
    {
        // 将本组件绑定的 obj 的 up 方向, 当成 重力系统的 up方向;
		Vector3 up = transform.up;
        float distance = Vector3.Dot(up, position - transform.position);// 只考虑重力方向的距离
		if (distance > range) 
        {
			return Vector3.zero;
		}

        float g = -gravity;
		if (distance > 0f) 
        {
			g *= 1f - distance / range; // 让重力随距离衰减
		}
		return g * up;

		//return -gravity * up;
	}


    void OnDrawGizmos () 
    {
        Vector3 scale = transform.localScale;
		scale.y = range;
        //Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);
        Vector3 size = new Vector3(1f, 0f, 1f);
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube( Vector3.zero, size );
        if (range > 0f) { // 青色
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.up, size);
        }
	}
}
