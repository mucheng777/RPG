using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class FloorGridLines : MonoBehaviour
{
    public float sizeX = 20f;
    public float sizeZ = 20f;
    public float cell = 1f;
    public float yOffset = 0.02f;
    public Color color = Color.white;
    public float width = 0.03f;

    void Start()
    {
        float x0 = -sizeX / 2f, x1 = sizeX / 2f;
        float z0 = -sizeZ / 2f, z1 = sizeZ / 2f;
        float y = yOffset;

        var pts = new System.Collections.Generic.List<Vector3>();
        for (float x = x0; x <= x1 + 0.0001f; x += cell)
        { pts.Add(new Vector3(x, y, z0)); pts.Add(new Vector3(x, y, z1)); }
        for (float z = z0; z <= z1 + 0.0001f; z += cell)
        { pts.Add(new Vector3(x0, y, z)); pts.Add(new Vector3(x1, y, z)); }

        var lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;          // 跟随父物体本地坐标
        lr.loop = false;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.positionCount = pts.Count;
        lr.SetPositions(pts.ToArray());
        // 材质用内置线材质：Project创建Material，Shader选 Sprites/Default 或 Particles/Standard Unlit
    }
}