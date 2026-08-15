using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;   // 如果不使用数学库也可以删掉
using UnityEngine;
using UnityEngine.InputSystem;
public class ThirdPersonMode : MonoBehaviour
{
    private CharacterController _controller;
    private GameObject _mainCamera;

    public float speed = 6.0f;
    float _targetRot = 0.0f;

    public float RotationSmoothTime = 0.01f;
    float _rotationVelocity;

    void Start()
    {
        if(_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        _controller = GetComponent<CharacterController>();    
    }

    void Update()
    {
        Vector3 velocity = new Vector3(0,-1,0);
        if(_move != Vector2.zero)
        {
            Vector3 inputDir = new Vector3(_move.x,0.0f,_move.y).normalized;

            _targetRot = Mathf.Atan2(inputDir.x,inputDir.z)*Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;

            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y,_targetRot,ref _rotationVelocity,RotationSmoothTime);

            transform.rotation = Quaternion.Euler(0.0f,rotation,0.0f);
            Vector3 targetDir = Quaternion.Euler(0.0f,_targetRot,0.0f)*Vector3.forward;
            velocity += targetDir.normalized * speed;
        } 
        //移动玩家
        _controller.Move(velocity);
    }

    Vector2 _move;
    void OnMove(InputValue value)
    {
        _move = value.Get<Vector2>();
    }
}
