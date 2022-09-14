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

	[SerializeField, Range(0f, 90f)]
	float alignSmoothRange = 45f;

	[SerializeField]
	LayerMask obstructionMask = -1; // 应该不包含 Details, 这样一些细节就不会被相机判定为墙体


    Vector2 orbitAngles = new Vector2(45f, 0f);// 沿 x轴 向下旋转 45度, 俯视;
    Vector3 focusPoint, previousFocusPoint; // 本帧/上一帧 相机瞄准的 pos;
    float lastManualRotationTime; // 上次手动 控制相机视角 的时间, 在一段时间内, 禁止 自动系统 介入;
	Camera regularCamera;


	Vector3 CameraHalfExtends 
	{
		get{
			Vector3 halfExtends;
			halfExtends.y =
				regularCamera.nearClipPlane *
				Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
			halfExtends.x = halfExtends.y * regularCamera.aspect;
			halfExtends.z = 0f;
			return halfExtends;
		}
	}


	void Awake () 
    {
		regularCamera = GetComponent<Camera>();
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


		Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane; // 从 camera pos 沿着视线前进到 near plane 的这段向量
		Vector3 rectPosition = lookPosition + rectOffset; // 上述向量在 near plane 上的点;
		Vector3 castFrom = focus.position; // 小球本帧 pos
		Vector3 castLine = rectPosition - castFrom; // 小球pos -> near plane 点;
		float castDistance = castLine.magnitude;
		Vector3 castDirection = castLine / castDistance; // 归一化

		// 避免相机进入墙体
		// details 无需被 判断为墙体, 否则相机会频繁地推拉
		if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask )) 
		{
			rectPosition = castFrom + castDirection * hit.distance;
			lookPosition = rectPosition - rectOffset;

			//lookPosition = focusPoint - lookDirection * (hit.distance + regularCamera.nearClipPlane);
		}


		transform.SetPositionAndRotation(lookPosition, lookRotation);

		//Update_Test();
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

		float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));

		float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));

		float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);

		if (deltaAbs < alignSmoothRange) // 本帧 转向角度 过小
		{
			rotationChange *= deltaAbs / alignSmoothRange;
		}
		else if (180f - deltaAbs < alignSmoothRange) // 本帧 向相机的反方向 (附件的角度) 旋转
		{
			rotationChange *= (180f - deltaAbs) / alignSmoothRange;
		}

		// 平滑每一帧 相机旋转的幅度;
		orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);
		
		return true;
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




	// 参数: direction: 本帧运动方向, xz平面;
	// 将这个方向转换为一个 角度值, 基于 xz平面的 +z方向, 向左转的角度为 负值, 向右的为正值;
    static float GetAngle (Vector2 direction) 
    {
		// 此处的 y 就是 xz 平面的 z; 
		float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
		// 小于0时, 从 +z 向左转, (逆时针)
		// 大于0时, 从 +z 向右转, (顺时针)
		return direction.x < 0f ? 360f - angle : angle;
	}




	int count_test = 0;
	void Update_Test()
	{
		count_test++;
		if( count_test%20!=0 )
		{	
			return;
		}

		Vector3 r = transform.TransformDirection( 1f, 0f, 0f );
		Debug.Log( r.ToString() );

	}


}

