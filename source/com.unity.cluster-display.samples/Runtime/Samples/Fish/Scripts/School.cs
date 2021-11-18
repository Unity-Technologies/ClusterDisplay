using Unity.ClusterDisplay;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.ClusterDisplay.RPC;

public class School : SingletonMonoBehaviour<School>
{
    [SerializeField] private PoolablePrefab fishPrefab = null;
    [SerializeField] private int startingSchoolSize = 5;

    [SerializeField] private SchoolTarget schoolTarget = null;

    private Fish[] fish = new Fish[0];
    private int fishCount = 0;

    private Transform[] fishTransforms = new Transform[0];

    public float skirtDistance = 20;

    private Bounds previousSchoolBounds;
    private Bounds schoolBounds;

    public Bounds SchoolBounds => schoolBounds;
    public Vector3 Center => FishUtils.CheckForNANs(schoolBounds.center);
    public Vector3 Velocity => Center - FishUtils.CheckForNANs(previousSchoolBounds.center);

    private SchoolJob schoolJob = new SchoolJob();
    private JobHandle ? schoolJobHandle = null;

    private UpdateMatricesJob updateMatricesJob = new UpdateMatricesJob();
    private NativeList<int> eatenFood;

    private void Awake() => UnityEngine.Random.InitState(100);

    private void OnDestroy()
    {
        if (eatenFood.IsCreated)
            eatenFood.Dispose();

        if (schoolJob.fishSpeeds.IsCreated)
            schoolJob.fishSpeeds.Dispose();

        if (schoolJob.pitchYawRoll.IsCreated)
            schoolJob.pitchYawRoll.Dispose();

        if (schoolJob.foodPositions.IsCreated)
            schoolJob.foodPositions.Dispose();

        if (schoolJob.foodAvailable.IsCreated)
            schoolJob.foodAvailable.Dispose();

        if (updateMatricesJob.fishMatrices.IsCreated)
            updateMatricesJob.fishMatrices.Dispose();
    }

    private void ResizeArray<T> (ref T[] array, int newSize)
    {
        T[] tempArray = new T[array.Length];
        System.Array.Copy(array, tempArray, array.Length);
        array = new T[newSize];
        System.Array.Copy(tempArray, array, tempArray.Length);
    }

    [ClusterRPC(RPCExecutionStage.BeforeUpdate)]
    public void SpawnFish(Vector3 position, Quaternion rotation) => SpawnLocally(position, rotation);

    public void SpawnLocally (Vector3 position, Quaternion rotation)
    {
        if (!PrefabPool.TryGetInstance(out var prefabPool))
            return;

        var newFish = prefabPool.Spawn(fishPrefab, parent: transform, setActive: true);
        if (fishCount + 1 > fish.Length)
        {
            ResizeArray(ref fish, fish.Length + 1);
            ResizeArray(ref fishTransforms, fishTransforms.Length + 1);
        }

        newFish.transform.position = position;
        newFish.transform.rotation = rotation;

        fish[fishCount] = newFish.GetComponent<Fish>();
        fishTransforms[fishCount] = newFish.transform;
        fishCount++;

        PollSchoolBuffers();
    }

    public void SetFishPositions (Vector3[] positions)
    {
        for (int i = 0; i < positions.Length; i++)
            fish[i].transform.position = positions[i];
    }

    private void ResetFishPositions ()
    {
        var startingFishPositions = new Vector3[startingSchoolSize];
        for (int i = 0; i < startingFishPositions.Length; i++)
            startingFishPositions[i] = Vector3.one;

        SetFishPositions(startingFishPositions);
    }
    
    public void PollSchoolBuffers()
    {
        schoolJob.fishCount = fishCount;
        if (fishCount < schoolJob.bufferSize)
            return;

        int preallocatedCount = 64;

        if (schoolJob.fishSpeeds.IsCreated)
            schoolJob.fishSpeeds.Dispose();

        if (schoolJob.pitchYawRoll.IsCreated)
            schoolJob.pitchYawRoll.Dispose();

        if (updateMatricesJob.fishMatrices.IsCreated)
            updateMatricesJob.fishMatrices.Dispose();

        schoolJob.fishSpeeds = new NativeArray<float>(fishCount + preallocatedCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        schoolJob.pitchYawRoll = new NativeArray<float3>(fishCount + preallocatedCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        updateMatricesJob.fishMatrices = new NativeArray<float4x4>(fishCount + preallocatedCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        schoolJob.fishMatrices = updateMatricesJob.fishMatrices.AsReadOnly();

        schoolJob.bufferSize = fishCount + preallocatedCount;
    }

    private void Update()
    {
        if (ClusterDisplayState.IsEmitter)
        {
            if (Input.GetKeyDown(KeyCode.R))
                ResetFishPositions();

            if ((Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0)) || (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Space)))
                SpawnFish(FishUtils.GetWorldInteractionPosition(), Quaternion.identity);
        }
    }

    private void FixedUpdate ()
    {
        SubmitData();
        TickSchool();

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        for (int i = 0; i < fishCount; i++)
        {
            fish[i].RB.AddTorque(schoolJob.fishMatrices[i].c2.xyz * schoolJob.pitchYawRoll[i].z, ForceMode.Force);
            fish[i].RB.AddTorque(schoolJob.fishMatrices[i].c1.xyz * schoolJob.pitchYawRoll[i].y, ForceMode.Force);
            fish[i].RB.AddTorque(schoolJob.fishMatrices[i].c0.xyz * schoolJob.pitchYawRoll[i].x, ForceMode.Force);
            fish[i].RB.AddForce(schoolJob.fishMatrices[i].c2.xyz * schoolJob.fishSpeeds[i], ForceMode.Force);

            min = Vector3.Min(schoolJob.fishMatrices[i].c3.xyz, min);
            max = Vector3.Max(schoolJob.fishMatrices[i].c3.xyz, max);
        }

        previousSchoolBounds = schoolBounds;
        schoolBounds = new Bounds((min + max) / 2f, max - min);

        DequeueFoodToEat();
    }

    public void TickSchool()
    {
        using (TransformAccessArray fishTransformAccessArray = new TransformAccessArray(fishTransforms))
        {
            var updateMatricesJobHandle = updateMatricesJob.Schedule(fishTransformAccessArray);
            schoolJobHandle = schoolJob.Schedule(fishCount, 1, updateMatricesJobHandle);
            schoolJobHandle.Value.Complete();
        }
    }

    private void SubmitData ()
    {
        schoolJob.skirtDistance = skirtDistance;
        schoolJob.schoolPosition = schoolTarget.Position;
        schoolJob.schoolRotation = schoolTarget.Rotation;

        schoolJob.foodPositions = World.FoodPositions;
        schoolJob.foodAvailable = World.FoodAvailable;

        if (!eatenFood.IsCreated || eatenFood.Length != schoolJob.foodPositions.Length)
        {
            if (eatenFood.IsCreated)
                eatenFood.Dispose();

            eatenFood = new NativeList<int>(schoolJob.foodPositions.Length, Allocator.Persistent);
            schoolJob.eatenFood =  eatenFood.AsParallelWriter();
        }
    }

    private void DequeueFoodToEat ()
    {
        if (eatenFood.IsCreated && eatenFood.Length > 0)
        {
            for (int i = 0; i < eatenFood.Length; i++)
            {
                float3 position = schoolJob.foodPositions[eatenFood[i]];
                if (World.EatFood(eatenFood[i]))
                    SpawnLocally(position, Quaternion.AngleAxis((RandomWrapper.value * 2.0f - 1.0f) * 180.0f, Vector3.forward));
            }

            PollSchoolBuffers();
            eatenFood.Clear();
        }
    }


    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
    public struct UpdateMatricesJob : IJobParallelForTransform
    {
        public NativeArray<float4x4> fishMatrices;
        public void Execute(int index, TransformAccess transform) => fishMatrices[index] = transform.localToWorldMatrix;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
    public struct SchoolJob : IJobParallelFor
    {
        [ReadOnly] public int bufferSize;
        [ReadOnly] public int fishCount;

        [ReadOnly] public float3 schoolPosition;
        [ReadOnly] public quaternion schoolRotation;
        [ReadOnly] public float skirtDistance;

        public NativeArray<float> fishSpeeds;
        [ReadOnly] public NativeArray<float4x4>.ReadOnly fishMatrices;
        public NativeArray<float3> pitchYawRoll;

        [ReadOnly] public NativeArray<float3> foodPositions;
        [ReadOnly] public NativeArray<bool> foodAvailable;
        public NativeList<int>.ParallelWriter eatenFood;

        public void Execute(int fishIndex)
        {
            var fishToTargetSchoolPosition = schoolPosition - fishMatrices[fishIndex].c3.xyz;
            float distanceToSchoolCenter = math.length(fishToTargetSchoolPosition);

            var schoolCenterDir = math.normalize(fishToTargetSchoolPosition);
            var targetFishDir = schoolCenterDir;
            
            var up = math.rotate(schoolRotation, new float3(0, 1, 0));
            var skirtDir = math.normalize(math.cross(schoolCenterDir, up));

            {
                float distancePercent = 1f - math.clamp(distanceToSchoolCenter / skirtDistance, 0f, 1f);
                targetFishDir = math.lerp(targetFishDir, skirtDir, distancePercent);
            }

            var dirSum = float3.zero;
            int dirCount = 0;

            for (int otherFishIndex = 0; otherFishIndex < fishCount; otherFishIndex++)
            {
                if (fishIndex == otherFishIndex)
                    continue;

                var otherFishPosition = fishMatrices[otherFishIndex].c3.xyz;
                var dirToOtherFish = otherFishPosition - fishMatrices[fishIndex].c3.xyz + new float3(0.0001f, 0.0001f, 0.0001f);

                float distanceToOtherFish = math.length(dirToOtherFish);
                if (distanceToOtherFish < 3f)
                {
                    float distancePercent = 1f - math.clamp(distanceToOtherFish / 3f, 0f, 1f);
                    float percentToCollision = math.clamp(distancePercent / 0.5f, 0f, 1f);

                    var avoidDir = (-math.normalize(dirToOtherFish) + math.normalize(fishMatrices[fishIndex].c2.xyz + new float3(0.0001f, 0.0001f, 0.0001f)));

                    dirSum += math.lerp(
                        targetFishDir,
                        avoidDir,
                        percentToCollision);

                    dirCount++;
                }
            }

            for (int foodIndex = 0; foodIndex < foodAvailable.Length; foodIndex++)
            {
                if (!foodAvailable[foodIndex])
                    continue;

                float3 foodDir = foodPositions[foodIndex] - fishMatrices[fishIndex].c3.xyz;
                float foodDistance = math.length(foodDir);

                if (foodDistance > 6f)
                    continue;

                if (foodDistance < 1f)
                {
                    eatenFood.AddNoResize(foodIndex);
                    break;
                }

                dirSum = math.normalize(foodDir);
                dirCount = 1;
            }

            if (dirCount > 0)
            {
                if (dirCount == 1)
                    targetFishDir = dirSum;
                else targetFishDir = math.normalize((dirSum / (float)dirCount));
            }

            float roll = math.dot(fishMatrices[fishIndex].c0.xyz, -up);
            float yaw = math.dot(fishMatrices[fishIndex].c0.xyz, targetFishDir);
            float pitch = math.dot(-fishMatrices[fishIndex].c1.xyz, targetFishDir);

            pitchYawRoll[fishIndex] = new float3(pitch, yaw, roll);
            fishSpeeds[fishIndex] = 1f;
        }
    }
}
