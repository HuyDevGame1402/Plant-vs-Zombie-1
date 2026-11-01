using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling; // Thư viện bắt buộc để dùng ProfilerMarker

public class ProfilerTest : MonoBehaviour
{
    private List<string> stringList = new List<string>();

    // 1. Khai báo các Marker riêng biệt cho từng hàm cần đo.
    // Dùng 'static readonly' để Marker không tạo thêm rác bộ nhớ (GC Alloc).
    static readonly ProfilerMarker s_GarbageMarker = new ProfilerMarker("MyTest.SimulateGarbage");
    static readonly ProfilerMarker s_FindMarker = new ProfilerMarker("MyTest.SimulateFind");
    static readonly ProfilerMarker s_MathMarker = new ProfilerMarker("MyTest.SimulateMath");

    void Update()
    {
        // 2. Bọc các hàm trong khối using (s_Marker.Auto()) để đo
        using (s_GarbageMarker.Auto())
        {
            SimulateGarbageCollection();
        }

        using (s_FindMarker.Auto())
        {
            SimulateHeavyFind();
        }

        using (s_MathMarker.Auto())
        {
            SimulateHeavyMath();
        }
    }

    void SimulateGarbageCollection()
    {
        // Ghép chuỗi trong Update làm phát sinh GC Alloc liên tục
        string testText = "";
        for (int i = 0; i < 50; i++)
        {
            testText += "Profiler Test " + i.ToString();
        }

        // Tạo mảng mới liên tục
        byte[] dummyBytes = new byte[1024];
    }

    void SimulateHeavyFind()
    {
        // GameObject.Find / FindObjectsByType rất tốn CPU khi gọi mỗi frame
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
    }

    void SimulateHeavyMath()
    {
        // Vòng lặp tính toán nặng tốn Self Time
        float result = 0;
        for (int i = 0; i < 20000; i++)
        {
            result += Mathf.Sin(i) * Mathf.Cos(i) * Mathf.Sqrt(i);
        }
    }
}