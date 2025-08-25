using System;
using DamageSystem;
using UnityEngine;

public class BreakArea : MonoBehaviour
{
    [SerializeField] private float breakForce = 10.0f;
    [SerializeField] private float minimumBreakVelocity = 5.0f; // 최소 파괴 속도
    
    private void OnCollisionEnter(Collision collision)
    {
        Damageable damageable = collision.gameObject.GetComponent<Damageable>();
        if (damageable == null) return;
        
        float impactVelocity = collision.relativeVelocity.magnitude;
        if (impactVelocity < minimumBreakVelocity) return;
        
        // 충돌 정보로부터 DamageInfo 생성
        DamageInfo damageInfo = new DamageInfo();
        
        // 충돌 지점 설정
        ContactPoint contact = collision.contacts[0];
        damageInfo.hitPoint = contact.point;
        
        // 충돌 방향 설정 (충돌한 객체 방향으로)
        damageInfo.hitDir = (collision.transform.position - transform.position).normalized;
        
        // 충돌 속도를 기반으로 한 힘 계산
        damageInfo.hitForce = Mathf.Max(breakForce, impactVelocity * breakForce);
        
        // Breakable 객체에 데미지 적용
        damageable.DoDamage(damageInfo);
        
        Debug.Log($"객체 파괴! 충돌 속도: {impactVelocity:F2}");
    }


    private void OnTriggerEnter(Collider other)
    {
        
    }
}
