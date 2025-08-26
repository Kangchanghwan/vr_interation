using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;

public class VRThrowSystemAdvanced : MonoBehaviour
{
    [Header("컴포넌트")]
    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rigidbody;
    
    [Header("던지기 설정")]
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private ForceMode forceMode = ForceMode.VelocityChange;
    [SerializeField] private bool useControllerDirection = true; // true: 컨트롤러 방향, false: 카메라 방향
    
    [Header("VR 컨트롤러 설정")]
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private InputFeatureUsage<bool> throwButton = CommonUsages.triggerButton; // A/X 버튼
    
    
    [Header("디버그")]
    [SerializeField] private bool showDebugRay = true;
    [SerializeField] private bool showControllerInfo = false;
    
    private Camera playerCamera;
    private XRBaseInteractor currentInteractor; // 현재 잡고 있는 인터랙터
    private bool buttonWasPressedLastFrame = false; // 버튼 중복 입력 방지

    private void Start()
    {
        // 컴포넌트 초기화
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody = GetComponent<Rigidbody>();
        playerCamera = Camera.main;
        
        // XR Grab 이벤트 연결
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);
        }
        
        // 카메라가 없으면 찾기
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
        
        Debug.Log($"VR 던지기 시스템 초기화 완료 - 컨트롤러: {controllerNode}, 버튼: {throwButton}");
    }

    private void Update()
    {
        if (!IsGrabbed()) return;
        
        // VR 컨트롤러 버튼 입력 처리 (우선순위)
        bool controllerButtonPressed = IsControllerButtonPressed();
        
        if (controllerButtonPressed && !buttonWasPressedLastFrame)
        {
            Debug.Log("VR 컨트롤러 버튼으로 던지기!");
            ThrowObject();
        }
        
        
        // 이전 프레임 버튼 상태 저장
        buttonWasPressedLastFrame = controllerButtonPressed;
        
        // 디버그 정보 표시
        if (showControllerInfo)
        {
            DisplayControllerDebugInfo();
        }
        
        // 디버그 레이 표시
        if (showDebugRay)
        {
            Vector3 throwDirection = GetThrowDirection();
            Debug.DrawRay(transform.position, throwDirection * 2f, Color.red);
        }
    }

    // 객체가 잡혔을 때 호출
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        currentInteractor = args.interactorObject as XRBaseInteractor;
        
        // 어느 손으로 잡았는지 자동 감지
        if (currentInteractor != null)
        {
            // 컨트롤러 이름으로 왼손/오른손 판단
            string controllerName = currentInteractor.name.ToLower();
            if (controllerName.Contains("left"))
            {
                controllerNode = XRNode.LeftHand;
                Debug.Log("왼손 컨트롤러로 잡혔습니다!");
            }
            else if (controllerName.Contains("right"))
            {
                controllerNode = XRNode.RightHand;
                Debug.Log("오른손 컨트롤러로 잡혔습니다!");
            }
        }
    }

    // 객체가 놓였을 때 호출
    private void OnReleased(SelectExitEventArgs args)
    {
        currentInteractor = null;
        buttonWasPressedLastFrame = false;
        Debug.Log($"{gameObject.name}이(가) 놓였습니다.");
    }

    // 현재 잡힌 상태인지 확인
    private bool IsGrabbed()
    {
        return _grabInteractable != null && _grabInteractable.isSelected && currentInteractor != null;
    }

    // VR 컨트롤러 버튼 입력 확인
    private bool IsControllerButtonPressed()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        
        if (device.isValid && device.TryGetFeatureValue(throwButton, out bool buttonPressed))
        {
            return buttonPressed;
        }
        
        return false;
    }

    // 던질 방향 계산
    private Vector3 GetThrowDirection()
    {
        if (useControllerDirection && currentInteractor != null)
        {
            // 컨트롤러(인터랙터)의 forward 방향 사용
            return currentInteractor.transform.forward;
        }
        else if (playerCamera != null)
        {
            // 플레이어 카메라의 forward 방향 사용
            return playerCamera.transform.forward;
        }
        else
        {
            // 기본값: 월드 forward 방향
            return Vector3.forward;
        }
    }

    // 객체 던지기 실행
    private void ThrowObject()
    {
        if (!IsGrabbed() || _rigidbody == null)
        {
            Debug.LogWarning("던지기 실패: 객체가 잡혀있지 않거나 Rigidbody가 없습니다.");
            return;
        }

        // 던질 방향 계산
        Vector3 throwDirection = GetThrowDirection().normalized;
        
        // 컨트롤러 속도 추가 (더 자연스러운 던지기)
        Vector3 controllerVelocity = GetControllerVelocity();
        
        // 먼저 객체를 놓기 (grab 해제)
        _grabInteractable.interactionManager.CancelInteractableSelection(_grabInteractable as IXRSelectInteractable);
        
        // 잠깐 대기 후 힘 적용 (grab 해제 완료 대기)
        Invoke(nameof(ApplyThrowForce), 0.02f);
        
        // 던질 방향과 속도 저장 (Invoke에서 사용)
        throwDirectionCache = throwDirection;
        controllerVelocityCache = controllerVelocity;
        
        Debug.Log($"{gameObject.name}을(를) {throwDirection} 방향으로 던집니다! (힘: {throwForce})");
    }
    
    // 캐시된 던질 정보
    private Vector3 throwDirectionCache;
    private Vector3 controllerVelocityCache;
    
    // 컨트롤러 속도 가져오기
    private Vector3 GetControllerVelocity()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        
        if (device.isValid && device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity))
        {
            return velocity;
        }
        
        return Vector3.zero;
    }
    
    // 실제 힘 적용 (Invoke로 호출)
    private void ApplyThrowForce()
    {
        if (_rigidbody != null)
        {
            // 기존 속도 초기화 (선택사항)
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            
            // 던지기 힘 + 컨트롤러 속도 적용
            Vector3 totalForce = (throwDirectionCache * throwForce) + (controllerVelocityCache * 2f);
            _rigidbody.AddForce(totalForce, forceMode);
            
            // 약간의 회전도 추가 (더 자연스러운 효과)
            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(-2f, 2f)
            );
            _rigidbody.AddTorque(randomTorque, ForceMode.Impulse);
            
            Debug.Log($"던지기 적용됨! 방향: {throwDirectionCache}, 컨트롤러 속도: {controllerVelocityCache}");
        }
    }

    // 컨트롤러 디버그 정보 표시
    private void DisplayControllerDebugInfo()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        
        if (device.isValid)
        {
            // 버튼 상태
            device.TryGetFeatureValue(throwButton, out bool buttonPressed);
            
            // 위치와 속도
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);
            
            string info = $"컨트롤러 [{controllerNode}]: 버튼={buttonPressed}, 속도={velocity.magnitude:F2}";
            
            // 화면에 텍스트로 표시 (임시)
            if (showControllerInfo)
            {
                Debug.Log(info);
            }
        }
        else
        {
            Debug.LogWarning($"컨트롤러 [{controllerNode}]을 찾을 수 없습니다!");
        }
    }

    // Inspector에서 던지기 테스트 (디버그용)
    [ContextMenu("던지기 테스트")]
    public void TestThrow()
    {
        if (Application.isPlaying)
        {
            ThrowObject();
        }
    }

    // 컨트롤러 노드 변경 (런타임에서)
    public void SetControllerNode(XRNode newNode)
    {
        controllerNode = newNode;
        Debug.Log($"컨트롤러 노드가 {newNode}로 변경되었습니다.");
    }

    // 던지기 버튼 변경 (런타임에서)
    public void SetThrowButton(InputFeatureUsage<bool> newButton)
    {
        throwButton = newButton;
        Debug.Log($"던지기 버튼이 변경되었습니다.");
    }

    // 던지기 힘 설정 변경 (런타임에서)
    public void SetThrowForce(float newForce)
    {
        throwForce = Mathf.Max(0f, newForce);
        Debug.Log($"던지기 힘이 {throwForce}로 변경되었습니다.");
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    // Gizmo로 던질 방향 시각화 (Scene 뷰에서)
    private void OnDrawGizmosSelected()
    {
        if (IsGrabbed())
        {
            Gizmos.color = Color.red;
            Vector3 direction = GetThrowDirection();
            Gizmos.DrawRay(transform.position, direction * 3f);
            Gizmos.DrawWireSphere(transform.position + direction * 3f, 0.1f);
        }
    }
}