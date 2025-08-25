using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EzySlice;
using DamageSystem;

public class VoronoiBreakable : Damageable
{
    [System.Serializable]
    public struct SlicePlane
    {
        public Vector3 point;
        public Vector3 normal;
        
        public SlicePlane(Vector3 point, Vector3 normal)
        {
            this.point = point;
            this.normal = normal;
        }
    }
    
    [SerializeField] private Material crossSectionMaterial;
    [SerializeField] private int fragmentCount = 8;
    [SerializeField] private float fragmentLifetime = 1f;
    [SerializeField] private float minimumFragmentSize = 0.1f; // 최소 조각 크기
    [SerializeField] private int minimumVertexCount = 8; // 최소 정점 수

    private List<GameObject> objectsToDestroy = new List<GameObject>(); // 안전한 삭제를 위한 목록

    public override void DoDamage(DamageInfo info)
    {
        // 물리 콜백 중이므로 코루틴으로 지연 실행
        StartCoroutine(DelayedVoronoiBreak(info));
    }

    private IEnumerator DelayedVoronoiBreak(DamageInfo info)
    {
        // 한 프레임 대기 (물리 콜백 종료 후 실행)
        yield return null;
        VoronoiBreak(info);
    }

    private void VoronoiBreak(DamageInfo info)
    {
        // 보로노이 시드 포인트들 생성
        Vector3[] seedPoints = GenerateVoronoiSeeds(info.hitPoint, fragmentCount);
        
        // 각 시드 포인트를 기준으로 분할 평면들 생성
        List<GameObject> fragments = new List<GameObject> { gameObject };
        
        for (int i = 0; i < seedPoints.Length; i++)
        {
            List<GameObject> newFragments = new List<GameObject>();
            
            foreach (GameObject fragment in fragments)
            {
                if (fragment == null) continue;
                
                // 인접한 시드들과의 중점을 기준으로 분할 평면 생성
                var slicePlanes = GetSlicePlanesForSeed(seedPoints, i, fragment.transform.position);
                
                GameObject currentFragment = fragment;
                
                foreach (var plane in slicePlanes)
                {
                    var slicedPieces = SliceWithPlane(currentFragment, plane.point, plane.normal);
                    
                    if (slicedPieces.Count > 0)
                    {
                        // 시드에 가까운 조각 선택
                        currentFragment = SelectClosestFragment(slicedPieces, seedPoints[i]);
                        
                        // 나머지 조각들은 삭제 목록에 추가
                        foreach (var piece in slicedPieces)
                        {
                            if (piece != currentFragment)
                                objectsToDestroy.Add(piece);
                        }
                    }
                }
                
                if (currentFragment != null)
                    newFragments.Add(currentFragment);
            }
            
            fragments = newFragments;
        }
        
        // 삭제할 객체들 정리
        CleanupObjects();
        
        // 물리 효과 적용
        ApplyFragmentPhysics(fragments, info);
        
        // 원본 비활성화
        gameObject.SetActive(false);
    }
    
    private void CleanupObjects()
    {
        foreach (GameObject obj in objectsToDestroy)
        {
            if (obj != null && obj != gameObject)
                Destroy(obj); // 안전한 Destroy 사용
        }
        objectsToDestroy.Clear();
    }
    
    private Vector3[] GenerateVoronoiSeeds(Vector3 center, int count)
    {
        Vector3[] seeds = new Vector3[count];
        Bounds bounds = GetComponent<Collider>().bounds;
        
        for (int i = 0; i < count; i++)
        {
            // 바운드 내 랜덤 포인트 생성
            seeds[i] = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }
        
        return seeds;
    }
    
    private List<SlicePlane> GetSlicePlanesForSeed(Vector3[] seeds, int currentSeedIndex, Vector3 fragmentCenter)
    {
        List<SlicePlane> planes = new List<SlicePlane>();
        Vector3 currentSeed = seeds[currentSeedIndex];
        
        // 다른 모든 시드들과 중점을 기준으로 평면 생성
        for (int i = 0; i < seeds.Length; i++)
        {
            if (i == currentSeedIndex) continue;
            
            Vector3 otherSeed = seeds[i];
            Vector3 midPoint = (currentSeed + otherSeed) * 0.5f;
            Vector3 normal = (currentSeed - otherSeed).normalized;
            
            planes.Add(new SlicePlane(midPoint, normal));
        }
        
        return planes;
    }
    
    private List<GameObject> SliceWithPlane(GameObject obj, Vector3 planePoint, Vector3 planeNormal)
    {
        List<GameObject> results = new List<GameObject>();
        
        // EzSlice를 사용한 분할
        SlicedHull hull = obj.Slice(planePoint, planeNormal, crossSectionMaterial);
        
        if (hull != null)
        {
            // 상부 조각
            GameObject upperHull = hull.CreateUpperHull(obj, crossSectionMaterial);
            if (upperHull != null && IsValidFragment(upperHull))
            {
                SetupFragment(upperHull);
                results.Add(upperHull);
            }
            else if (upperHull != null)
            {
                // 유효하지 않은 조각은 즉시 제거 (EzSlice가 생성한 것이므로 안전)
                Destroy(upperHull);
            }
            
            // 하부 조각
            GameObject lowerHull = hull.CreateLowerHull(obj, crossSectionMaterial);
            if (lowerHull != null && IsValidFragment(lowerHull))
            {
                SetupFragment(lowerHull);
                results.Add(lowerHull);
            }
            else if (lowerHull != null)
            {
                // 유효하지 않은 조각은 즉시 제거
                Destroy(lowerHull);
            }
        }
        
        return results;
    }
    
    // 조각이 유효한지 검사
    private bool IsValidFragment(GameObject fragment)
    {
        MeshFilter meshFilter = fragment.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
            return false;
        
        Mesh mesh = meshFilter.mesh;
        
        // 최소 정점 수 확인
        if (mesh.vertexCount < minimumVertexCount)
        {
            Debug.Log($"정점 수 부족: {mesh.vertexCount} < {minimumVertexCount}");
            return false;
        }
        
        // 최소 삼각형 수 확인
        if (mesh.triangles.Length < 12)
        {
            Debug.Log($"삼각형 수 부족: {mesh.triangles.Length / 3} < 4");
            return false;
        }
        
        // 바운드 크기 확인
        if (mesh.bounds.size.magnitude < minimumFragmentSize)
        {
            Debug.Log($"조각이 너무 작음: {mesh.bounds.size.magnitude} < {minimumFragmentSize}");
            return false;
        }
        
        // 정점들이 모두 같은 위치에 있는지 확인
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length > 0)
        {
            Vector3 firstVertex = vertices[0];
            bool allSamePosition = true;
            for (int i = 1; i < vertices.Length; i++)
            {
                if (Vector3.Distance(vertices[i], firstVertex) > 0.001f)
                {
                    allSamePosition = false;
                    break;
                }
            }
            
            if (allSamePosition)
            {
                Debug.Log("모든 정점이 같은 위치에 있음");
                return false;
            }
        }
        
        return true;
    }
    
    // 메시가 MeshCollider로 사용 가능한지 정밀 테스트
    private bool IsValidMeshForCollider(GameObject fragment)
    {
        MeshFilter meshFilter = fragment.GetComponent<MeshFilter>();
        if (meshFilter?.mesh == null) return false;
        
        Mesh mesh = meshFilter.mesh;
        
        // 기본 검사
        if (mesh.vertexCount < 4 || mesh.triangles.Length < 12)
            return false;
            
        Vector3[] vertices = mesh.vertices;
        
        // 1. 고유한 정점이 최소 4개 이상인지 확인
        List<Vector3> uniqueVertices = GetUniqueVertices(vertices);
        if (uniqueVertices.Count < 4)
        {
            Debug.Log($"고유 정점 수 부족: {uniqueVertices.Count} < 4");
            return false;
        }
        
        // 2. 정점들이 3D 공간에 분포되어 있는지 확인 (평면상에만 있으면 안됨)
        if (!IsVolumeMesh(uniqueVertices))
        {
            Debug.Log("정점들이 평면상에만 있음 - 3D 볼륨이 아님");
            return false;
        }
        
        // 3. 실제 PhysX 테스트 (가장 확실한 방법)
        return TestPhysXConvexCreation(mesh);
    }
    
    // 고유한 정점들만 추출 (중복 제거)
    private List<Vector3> GetUniqueVertices(Vector3[] vertices)
    {
        List<Vector3> unique = new List<Vector3>();
        float threshold = 0.001f; // 1mm 이하는 같은 점으로 간주
        
        foreach (Vector3 vertex in vertices)
        {
            bool isDuplicate = false;
            foreach (Vector3 existing in unique)
            {
                if (Vector3.Distance(vertex, existing) < threshold)
                {
                    isDuplicate = true;
                    break;
                }
            }
            
            if (!isDuplicate)
                unique.Add(vertex);
        }
        
        return unique;
    }
    
    // 정점들이 실제 3D 볼륨을 형성하는지 확인
    private bool IsVolumeMesh(List<Vector3> vertices)
    {
        if (vertices.Count < 4) return false;
        
        // 첫 3개 점으로 평면 정의
        Vector3 v1 = vertices[0];
        Vector3 v2 = vertices[1];
        Vector3 v3 = vertices[2];
        
        Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
        
        // 나머지 점들이 이 평면에서 벗어나는지 확인
        for (int i = 3; i < vertices.Count; i++)
        {
            float distance = Mathf.Abs(Vector3.Dot(vertices[i] - v1, normal));
            if (distance > 0.01f) // 1cm 이상 벗어나면 3D로 판단
            {
                return true;
            }
        }
        
        return false; // 모든 점이 평면상에 있음
    }
    
    // PhysX 엔진에서 실제로 Convex Hull을 생성할 수 있는지 테스트
    private bool TestPhysXConvexCreation(Mesh mesh)
    {
        // 임시 GameObject로 실제 테스트
        GameObject testObj = new GameObject("ConvexTest");
        testObj.transform.position = Vector3.zero;
        testObj.hideFlags = HideFlags.HideAndDontSave; // 인스펙터에 표시 안함
        
        try
        {
            MeshFilter testFilter = testObj.AddComponent<MeshFilter>();
            testFilter.mesh = mesh;
            
            MeshCollider testCollider = testObj.AddComponent<MeshCollider>();
            testCollider.convex = true; // 여기서 실패하면 catch로 이동
            
            // 성공적으로 생성됨
            DestroyImmediate(testObj);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PhysX Convex 생성 테스트 실패: {e.Message}");
            DestroyImmediate(testObj);
            return false;
        }
    }
    
    private GameObject SelectClosestFragment(List<GameObject> fragments, Vector3 targetPoint)
    {
        GameObject closest = null;
        float minDistance = float.MaxValue;
        
        foreach (GameObject fragment in fragments)
        {
            if (fragment == null) continue;
            
            float distance = Vector3.Distance(fragment.transform.position, targetPoint);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = fragment;
            }
        }
        
        return closest;
    }
    
    private void SetupFragment(GameObject fragment)
    {
        // Rigidbody 추가
        Rigidbody rb = fragment.GetComponent<Rigidbody>();
        if (rb == null)
            rb = fragment.AddComponent<Rigidbody>();
        
        // 다시 한번 유효성 검사 (EzSlice가 생성한 직후)
        if (!IsValidFragment(fragment))
        {
            Debug.LogWarning("조각이 유효하지 않음 - 삭제 예정");
            Destroy(fragment);
            return;
        }
        
        // 콜라이더 설정 (매우 안전하게)
        SetupSafeCollider(fragment);
        
        // 일정 시간 후 제거
        Destroy(fragment, fragmentLifetime);
    }
    
    // 매우 안전한 콜라이더 설정
    private void SetupSafeCollider(GameObject fragment)
    {
        // 1단계: 기본 검사로 MeshCollider 시도
        if (IsValidMeshForCollider(fragment))
        {
            if (TrySetupMeshCollider(fragment))
                return; // 성공하면 종료
        }
        
        // 2단계: MeshCollider 실패 시 BoxCollider 사용
        Debug.Log("MeshCollider 실패 - BoxCollider로 대체");
        SetupAlternativeCollider(fragment);
    }
    
    // MeshCollider 설정 시도 (실패 가능성 고려)
    private bool TrySetupMeshCollider(GameObject fragment)
    {
        try
        {
            // 기존 콜라이더 제거
            MeshCollider existingCol = fragment.GetComponent<MeshCollider>();
            if (existingCol != null)
                DestroyImmediate(existingCol);
            
            // 새 MeshCollider 추가
            MeshCollider col = fragment.AddComponent<MeshCollider>();
            col.convex = true;
            
            Debug.Log("MeshCollider 설정 성공");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MeshCollider 설정 실패: {e.Message}");
            
            // 실패한 MeshCollider 정리
            MeshCollider failedCol = fragment.GetComponent<MeshCollider>();
            if (failedCol != null)
                DestroyImmediate(failedCol);
                
            return false;
        }
    }
    
    private void SetupAlternativeCollider(GameObject fragment)
    {
        // BoxCollider로 대체
        BoxCollider boxCol = fragment.GetComponent<BoxCollider>();
        if (boxCol == null)
            boxCol = fragment.AddComponent<BoxCollider>();
        
        MeshFilter meshFilter = fragment.GetComponent<MeshFilter>();
        if (meshFilter?.mesh != null)
        {
            boxCol.size = meshFilter.mesh.bounds.size;
            boxCol.center = meshFilter.mesh.bounds.center;
        }
        
        Debug.Log("BoxCollider로 대체됨");
    }
    
    private void ApplyFragmentPhysics(List<GameObject> fragments, DamageInfo info)
    {
        foreach (GameObject fragment in fragments)
        {
            if (fragment == null) continue;
            
            Rigidbody rb = fragment.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 폭발력 적용
                rb.AddExplosionForce(info.hitForce, info.hitPoint, info.explosionRadius);
                
                // 랜덤 회전 추가
                rb.AddTorque(Random.insideUnitSphere * info.hitForce * 0.5f, ForceMode.Impulse);
            }
        }
    }
}