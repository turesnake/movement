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
	float focusRadius = 1f;// 若为 0, 相机始终盯着 本帧小球pos, 若大于0, 则盯着小球的 前帧pos 和 本帧pos 之间的某个pos;

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

	[SerializeField, Min(0f)]
	float upAlignmentSpeed = 360f;



	// 相机旋转信息: 沿 x轴(right向量) 旋转多少度 (俯仰), 沿 y轴(up向量) 旋转多少度(偏航);
    Vector2 orbitAngles = new Vector2(45f, 0f);// 沿 x轴 向下旋转 45度, 俯视;

    Vector3 focusPoint, previousFocusPoint; // 本帧/上一帧 相机瞄准的 pos;
    float lastManualRotationTime; // 上次手动 控制相机视角 的时间, 在一段时间内, 禁止 自动系统 介入;
	Camera regularCamera;

	// 从原始的 重力方向(-y) 到本帧重力方向(任意) 的 "旋转"信息;
	Quaternion gravityAlignment = Quaternion.identity;

	Quaternion orbitRotation;

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
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
	}


    void LateUpdate () 
    {

		UpdateGravityAlignment();
        UpdateFocusPoint();

        //Quaternion lookRotation;
		if ( ManualRotation() || AutomaticRotation() ) 
        {
			ConstrainAngles();
			orbitRotation = Quaternion.Euler(orbitAngles);
		}

		// right-up旋转, 叠加上 重力修正旋转
		Quaternion lookRotation = gravityAlignment * orbitRotation;

		Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;

		// 不能直接使用 focusPoint, 因为 focusPoint 并不正对 小球圆心;
		// 所以需要在本地再计算一下:
		Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane; // 从 camera pos 沿着视线前进到 near plane 的这段向量
		Vector3 rectPosition = lookPosition + rectOffset; // 上述向量在 near plane 上的点;
		Vector3 castFrom = focus.position; // 小球本帧 pos
		Vector3 castLine = rectPosition - castFrom; // 小球pos -> near plane 点;
		float castDistance = castLine.magnitude;
		Vector3 castDirection = castLine / castDistance; // 归一化

		// 避免相机进入墙体
		// details 无需被 判 断为墙体, 否则相机会频繁地推拉
		if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask )) 
		{
			rectPosition = castFrom + castDirection * hit.distance;
			lookPosition = rectPosition - rectOffset;
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


	void UpdateGravityAlignment () 
	{
		// 从 "last aligned up dir" -> "current up dir" 的最小 "旋转";
		// 再将上面这个 "旋转" 对象, 左乘到 旧的 "旋转"对象 上, 获得本帧 新的 "旋转"对象;
		Vector3 fromUp = gravityAlignment * Vector3.up;
		Vector3 toUp = CustomGravity.GetUpAxis(focusPoint);


		float dot = Mathf.Clamp(Vector3.Dot(fromUp, toUp), -1f, 1f);
		float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
		float maxAngle = upAlignmentSpeed * Time.deltaTime;

		Quaternion newAlignment = Quaternion.FromToRotation(fromUp, toUp) * gravityAlignment;

		//gravityAlignment = newAlignment;
		if (angle <= maxAngle) 
		{
			gravityAlignment = newAlignment;
		}
		else 
		{
			gravityAlignment = Quaternion.SlerpUnclamped(
				gravityAlignment, newAlignment, maxAngle / angle
			);
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

		// 先计算出 本帧小球的 位移向量; (ws), 注意, 如果重力方向是自定义的, 则这个位移向量可以是任意方向
		// 对此 位移向量 执行一个 "从 本帧重力方向 到 原始重力方向" 的 "旋转", 
		// 就能得到: 在本帧重力方向构成的坐标系下的 小球位移向量;
		Vector3 alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);

		Vector2 movement = new Vector2(alignedDelta.x, alignedDelta.z);

        // Vector2 movement = new Vector2(
		// 	focusPoint.x - previousFocusPoint.x,
		// 	focusPoint.z - previousFocusPoint.z
		// );

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

