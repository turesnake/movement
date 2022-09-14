using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    
    [SerializeField]
	Transform focus = default;

	[SerializeField, Range(1f, 20f)]
	float distance = 5f;

    [SerializeField, Min(0f)]
	float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
	float focusCentering = 0.5f; // 值越大, 物体停止后 相机跟随的速度越快

    [SerializeField, Range(1f, 360f)]
	float rotationSpeed = 90f;

    [SerializeField, Range(-89f, 89f)]
	float minVerticalAngle = -30f, maxVerticalAngle = 60f; // 限制相机俯仰角

    [SerializeField, Min(0f)]
	float alignDelay = 5f;


    Vector2 orbitAngles = new Vector2(45f, 0f);// 沿 x轴 向下旋转 45度, 俯视;
    Vector3 focusPoint, previousFocusPoint; // 本帧/上一帧 相机瞄准的 pos;
    float lastManualRotationTime; // 上次手动 控制相机视角 的时间, 在一段时间内, 禁止 自动系统 介入;


	void Awake () 
    {
		focusPoint = focus.position;
        transform.localRotation = Quaternion.Euler(orbitAngles);
	}


    void LateUpdate () 
    {
        UpdateFocusPoint();

        Quaternion lookRotation;
		if ( ManualRotation() || AutomaticRotation() ) 
        {
			ConstrainAngles();
			lookRotation = Quaternion.Euler(orbitAngles);
		}
		else 
        {
			lookRotation = transform.localRotation;
		}

		Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;
		transform.SetPositionAndRotation(lookPosition, lookRotation);
	}


    // 只在 editor 模式中有意义的函数, 当一个 脚本被 loaded, 或当一个变量在 inspector 中被修改时, 此函数被调用.
    void OnValidate () 
    {
        // 仅仅用来修正 用户设置的 参数;
		if (maxVerticalAngle < minVerticalAngle) 
        {
			maxVerticalAngle = minVerticalAngle;
		}
	}


    void UpdateFocusPoint () 
    {
        previousFocusPoint = focusPoint;
		Vector3 targetPoint = focus.position; // 本帧 目标 pos
        if (focusRadius > 0f) 
        {
			float distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;
			if (distance > 0.01f && focusCentering > 0f) 
            {
				//t = Mathf.Pow( 1f - focusCentering, Time.deltaTime );
                t = Mathf.Pow( 1f - focusCentering, Time.unscaledDeltaTime );
			}
			if (distance > focusRadius) 
            {
                t = Mathf.Min( t, focusRadius / distance );
                
			}
            focusPoint = Vector3.Lerp( targetPoint, focusPoint, t );
		}
		else 
        {
			focusPoint = targetPoint;
		}
	}


    bool ManualRotation () 
    {
		Vector2 input = new Vector2(
			Input.GetAxis("Vertical Camera"),
			Input.GetAxis("Horizontal Camera")
		);
		const float e = 0.001f;
		if (input.x < -e || input.x > e || input.y < -e || input.y > e) 
        {
			orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
		}
        return false;
	}


    void ConstrainAngles () 
    {
		orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

		if (orbitAngles.y < 0f) 
        {
			orbitAngles.y += 360f;
		}
		else if (orbitAngles.y >= 360f) 
        {
			orbitAngles.y -= 360f;
		}
	}


    bool AutomaticRotation () 
    {
		if (Time.unscaledTime - lastManualRotationTime < alignDelay) 
        {
			return false;
		}

        Vector2 movement = new Vector2(
			focusPoint.x - previousFocusPoint.x,
			focusPoint.z - previousFocusPoint.z
		);
		float movementDeltaSqr = movement.sqrMagnitude; // 距离的平方
		if (movementDeltaSqr < 0.0001f) 
        {
			return false; // 帧内运动距离太短, 无需调整
		}
		
		return true;
	}

    static float GetAngle (Vector2 direction) 
    {
		float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
		return angle;
	}



}

