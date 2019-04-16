using HZMsg;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LJParticles : MonoBehaviour
{

    public int RenderSplit = 500;
    private List<Matrix4x4[]> transformList = new List<Matrix4x4[]>();
    private Mesh mesh;
    private Material material;
    private int N = 0;

    public static IEnumerable<List<T>> splitList<T>(List<T> locations, int nSize = 500)
    {
        for (int i = 0; i < locations.Count; i += nSize)
        {
            yield return locations.GetRange(i, System.Math.Min(nSize, locations.Count - i));
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        var cc = GameObject.Find("CommClient").GetComponent<CommClient>();
        cc.OnNewFrame += ProcessFrameUpdate;
        mesh = GetComponent<MeshFilter>().mesh;
        material = GetComponent<Renderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < transformList.Count; i++)
        {            
            Graphics.DrawMeshInstanced(mesh, 0, material, transformList[i]);
        }
    }

    void ProcessFrameUpdate(Frame frame)
    {
        if(frame.N != N)
        {
            N = frame.N;
            var count = N;
            transformList = new List<Matrix4x4[]>(N / RenderSplit + 1);
            for (int i = 0; count > 0; i++)
            {
                transformList[i] = new Matrix4x4[System.Math.Min(count, RenderSplit)];
                count -= transformList[i].Length;
                for (int j = 0; j < RenderSplit; j++)
                {
                    if (i * RenderSplit + j == N)
                        break;
                    Matrix4x4 matrix = new Matrix4x4();
                    matrix.SetTRS(Vector3.zero, Quaternion.Euler(Vector3.zero), Vector3.one);
                    transformList[i][j] = matrix;
                }
            }            
        }

        for (int i = 0; i < transformList.Count; i++)
        {
            for(int j = 0; i * RenderSplit + j < N; j++)
            {
                transformList[i][j][0, 3] = frame.Positions(i).Value.X;
                transformList[i][j][1, 3] = frame.Positions(i).Value.Y;
                transformList[i][j][2, 3] = frame.Positions(i).Value.Z;
            }            
        }
    }
}
