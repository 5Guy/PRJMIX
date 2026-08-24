using System.Collections.Generic;
using UnityEngine;

// 원소 모델을 세울 때 쓰는 도형 공장.
//
// 유니티 기본 도형(정육면체·구·원기둥)만으로는 불꽃·결정·파도처럼 "이름이 읽히는" 모양을
// 만들 수 없다. 여기서 필요한 몇 가지를 직접 짜서 만든다.
// 만든 메시는 ElementModelBuilder가 에셋으로 저장해 프리팹에 물린다.
//
// 규칙
//   - 모든 도형은 원점을 밑면 한가운데에 둔다. 그대로 땅에 올려놓으면 바닥에 딱 붙는다.
//   - 크기는 지름 1, 높이 1 안팎으로 만든다. 실제 크기는 놓을 때 맞춘다.
public static class ElementMeshFactory
{
    // 옆면이 나뉜 원뿔. 산·불꽃·화산의 뼈대가 된다.
    //
    // topRadius를 주면 잘린 원뿔(위가 평평한 산)이 된다.
    public static Mesh Cone(string name, float bottomRadius, float topRadius, float height, int sides, bool capTop = true)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

            Vector3 b0 = new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius);
            Vector3 t0 = new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius);
            Vector3 t1 = new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius);

            AddQuad(vertices, triangles, b0, b1, t1, t0);

            // 밑면
            AddTriangle(vertices, triangles, Vector3.zero, b1, b0);

            if (capTop && topRadius > 0.0001f)
            {
                AddTriangle(vertices, triangles, new Vector3(0f, height, 0f), t0, t1);
            }
        }

        return Build(name, vertices, triangles);
    }

    // 밑동이 둥글고 위가 뾰족한 물방울. 불꽃과 물방울에 함께 쓴다.
    //
    // 원뿔로는 불꽃이 되지 않는다 — 옆선이 곧아서 고깔모자로 보인다.
    // 밑에서 조금 올라간 곳이 제일 볼록하고 위로 갈수록 홀쭉해지는 곡선이라야 불꽃으로 읽힌다.
    public static Mesh Teardrop(string name, float radius, float height, int sides, int rings, float sway)
    {
        const float Base = 0.35f;   // 밑동에서 이미 굵기가 붙어 있게 하는 값
        float bottomScale = Mathf.Sin(Mathf.PI * Base);

        Vector3 RingPoint(float t, float angle)
        {
            float profile = Mathf.Sin(Mathf.PI * (Base + (1f - Base) * t)) / bottomScale;
            float r = radius * profile;

            // 위로 갈수록 한쪽으로 살짝 기울여 불꽃이 흔들리는 느낌을 준다.
            float lean = sway * t * t;

            return new Vector3(Mathf.Cos(angle) * r + lean, height * t, Mathf.Sin(angle) * r);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int ring = 0; ring < rings; ring++)
        {
            float t0 = ring / (float)rings;
            float t1 = (ring + 1) / (float)rings;

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                AddQuad(vertices, triangles,
                    RingPoint(t0, a0), RingPoint(t0, a1), RingPoint(t1, a1), RingPoint(t1, a0));
            }
        }

        // 밑면을 막는다.
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            AddTriangle(vertices, triangles, Vector3.zero, RingPoint(0f, a1), RingPoint(0f, a0));
        }

        return Build(name, vertices, triangles);
    }

    // 위아래로 뾰족한 결정(흑요석·광석). 허리를 기준으로 위가 길다.
    public static Mesh Crystal(string name, float radius, float bottomHeight, float topHeight, int sides)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        Vector3 bottom = new Vector3(0f, 0f, 0f);
        Vector3 top = new Vector3(0f, bottomHeight + topHeight, 0f);

        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

            Vector3 w0 = new Vector3(Mathf.Cos(a0) * radius, bottomHeight, Mathf.Sin(a0) * radius);
            Vector3 w1 = new Vector3(Mathf.Cos(a1) * radius, bottomHeight, Mathf.Sin(a1) * radius);

            AddTriangle(vertices, triangles, bottom, w1, w0);
            AddTriangle(vertices, triangles, top, w0, w1);
        }

        return Build(name, vertices, triangles);
    }

    // 위로 갈수록 좁아지는 상자(쇳덩이). 대장간에서 뽑아낸 잉곳 모양이다.
    public static Mesh Ingot(string name, float width, float depth, float height, float taper)
    {
        float bx = width * 0.5f;
        float bz = depth * 0.5f;
        float tx = bx * taper;
        float tz = bz * taper;

        Vector3 b0 = new Vector3(-bx, 0f, -bz);
        Vector3 b1 = new Vector3(bx, 0f, -bz);
        Vector3 b2 = new Vector3(bx, 0f, bz);
        Vector3 b3 = new Vector3(-bx, 0f, bz);

        Vector3 t0 = new Vector3(-tx, height, -tz);
        Vector3 t1 = new Vector3(tx, height, -tz);
        Vector3 t2 = new Vector3(tx, height, tz);
        Vector3 t3 = new Vector3(-tx, height, tz);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        AddQuad(vertices, triangles, b0, b1, t1, t0);
        AddQuad(vertices, triangles, b1, b2, t2, t1);
        AddQuad(vertices, triangles, b2, b3, t3, t2);
        AddQuad(vertices, triangles, b3, b0, t0, t3);
        AddQuad(vertices, triangles, t0, t1, t2, t3);
        AddQuad(vertices, triangles, b3, b2, b1, b0);

        return Build(name, vertices, triangles);
    }

    // 위로 솟다가 한쪽으로 말려 넘어가는 물마루(쓰나미).
    //
    // 옆에서 본 곡선을 하나 그려 두고 그 선을 따라 폭이 있는 띠를 세운다.
    // 위로 갈수록 얇아지고 앞으로 말려서, 옆에서 보면 갈고리처럼 휘어 보인다.
    public static Mesh Wave(string name, float width, float height, float curl, int steps)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float half = width * 0.5f;

        Vector3 PointAt(float t, float thicknessScale)
        {
            // t = 0 밑동, 1 마루 끝. 위로 갈수록 앞(+z)으로 말린다.
            float y = height * Mathf.Sin(t * Mathf.PI * 0.5f);
            float z = curl * t * t * t;
            return new Vector3(0f, y, z + thicknessScale);
        }

        for (int i = 0; i < steps; i++)
        {
            float t0 = i / (float)steps;
            float t1 = (i + 1) / (float)steps;

            // 밑동은 두껍고 마루는 얇다.
            float d0 = Mathf.Lerp(0.16f, 0.03f, t0);
            float d1 = Mathf.Lerp(0.16f, 0.03f, t1);

            Vector3 back0 = PointAt(t0, -d0);
            Vector3 front0 = PointAt(t0, d0);
            Vector3 back1 = PointAt(t1, -d1);
            Vector3 front1 = PointAt(t1, d1);

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -half : half;
                Vector3 offset = new Vector3(x, 0f, 0f);

                if (side == 0)
                {
                    AddQuad(vertices, triangles,
                        back0 + offset, front0 + offset, front1 + offset, back1 + offset);
                }
                else
                {
                    AddQuad(vertices, triangles,
                        back1 + offset, front1 + offset, front0 + offset, back0 + offset);
                }
            }

            Vector3 left = new Vector3(-half, 0f, 0f);
            Vector3 right = new Vector3(half, 0f, 0f);

            // 앞면과 뒷면
            AddQuad(vertices, triangles, front0 + left, front0 + right, front1 + right, front1 + left);
            AddQuad(vertices, triangles, back0 + right, back0 + left, back1 + left, back1 + right);
        }

        // 마루 끝을 막는다.
        Vector3 capBack = PointAt(1f, -0.03f);
        Vector3 capFront = PointAt(1f, 0.03f);
        AddQuad(vertices, triangles,
            capBack + new Vector3(-half, 0f, 0f), capFront + new Vector3(-half, 0f, 0f),
            capFront + new Vector3(half, 0f, 0f), capBack + new Vector3(half, 0f, 0f));

        return Build(name, vertices, triangles);
    }

    // 밑동이 넓고 끝이 뾰족한 풀잎. 살짝 휘어 있다.
    public static Mesh Blade(string name, float width, float height, float bend, int steps)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < steps; i++)
        {
            float t0 = i / (float)steps;
            float t1 = (i + 1) / (float)steps;

            float w0 = width * (1f - t0) * 0.5f;
            float w1 = width * (1f - t1) * 0.5f;

            Vector3 c0 = new Vector3(0f, height * t0, bend * t0 * t0);
            Vector3 c1 = new Vector3(0f, height * t1, bend * t1 * t1);

            Vector3 a0 = c0 + Vector3.left * w0;
            Vector3 b0 = c0 + Vector3.right * w0;
            Vector3 a1 = c1 + Vector3.left * w1;
            Vector3 b1 = c1 + Vector3.right * w1;

            AddQuad(vertices, triangles, a0, b0, b1, a1);
            AddQuad(vertices, triangles, b0, a0, a1, b1);
        }

        return Build(name, vertices, triangles);
    }

    // 흙더미·재더미처럼 울퉁불퉁한 낮은 봉우리.
    //
    // 구를 눌러 놓은 것만으로는 너무 매끈해서 공처럼 보인다.
    // 봉우리마다 높이를 조금씩 흩뜨려 "쌓아 놓은 것" 느낌을 낸다.
    public static Mesh Mound(string name, float radius, float height, int sides, int rings, float roughness, int seed)
    {
        Random.State previous = Random.state;
        Random.InitState(seed);

        Vector3[,] grid = new Vector3[rings + 1, sides];

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;

                // 마지막 고리까지 가면 반지름이 0이 되어 봉우리가 뭉개진다.
                // 꼭대기 바로 아래에서 멈추고, 남은 자리는 아래에서 삼각형으로 덮는다.
                float shaped = t * 0.82f;

                float r = radius * Mathf.Cos(shaped * Mathf.PI * 0.5f);
                float y = height * Mathf.Sin(shaped * Mathf.PI * 0.5f);

                float noise = 1f + Random.Range(-roughness, roughness) * (1f - t * 0.4f);

                grid[ring, i] = new Vector3(
                    Mathf.Cos(angle) * r * noise,
                    y * noise,
                    Mathf.Sin(angle) * r * noise);
            }
        }

        Random.state = previous;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int ring = 0; ring < rings; ring++)
        {
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                AddQuad(vertices, triangles,
                    grid[ring, i], grid[ring, next], grid[ring + 1, next], grid[ring + 1, i]);
            }
        }

        // 꼭대기를 한 점으로 모은다.
        Vector3 peak = new Vector3(0f, height, 0f);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            AddTriangle(vertices, triangles, peak, grid[rings, i], grid[rings, next]);
        }

        // 밑면
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            AddTriangle(vertices, triangles, Vector3.zero, grid[0, next], grid[0, i]);
        }

        return Build(name, vertices, triangles);
    }

    // 웅덩이 둘레를 이루는 통제점. 열두 갈래로 나눠 두고 그 사이는 부드러운 곡선(Catmull-Rom)
    // 으로 잇는다.
    //
    // 처음에는 사인을 몇 개 더해 둘레를 흔들었는데, 그러면 혹이 일정한 간격으로 촘촘히
    // 반복되어 위에서 보면 톱니 달린 원 — 여전히 맨홀 뚜껑으로 보였다. 웅덩이답게 보이려면
    // 크기가 저마다 다른 혹 몇 개가 삐져나온 아메바 모양이어야 한다. 그래서 통제점 값을
    // 하나씩 손으로 얹었다: 큰 혹 하나(오른쪽 아래)가 두드러지고, 나머지는 크기가 들쭉날쭉
    // 하게. 난수를 쓰면 만들 때마다 모양이 달라져서 눈으로 맞춰 둔 모습이 유지되지 않는다.
    private static readonly float[] PuddleLobes =
    {
        1.06f, 1.02f, 1.14f, 0.98f, 0.86f, 0.96f, 1.04f, 0.92f, 0.84f, 0.98f, 1.16f, 0.96f,
    };

    private static float PuddleWobble(float angle, float turn)
    {
        int count = PuddleLobes.Length;
        float f = (angle + turn) / (Mathf.PI * 2f) * count;
        f = ((f % count) + count) % count;

        int i = (int)f;
        float t = f - i;

        float p0 = PuddleLobes[(i - 1 + count) % count];
        float p1 = PuddleLobes[i % count];
        float p2 = PuddleLobes[(i + 1) % count];
        float p3 = PuddleLobes[(i + 2) % count];

        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // 땅에 고인 물 한 방울. 물웅덩이의 몸통이 된다.
    //
    // 원반을 겹쳐서는 웅덩이가 되지 않는다 — 테두리가 정확한 원이라 위에서 보면 맨홀
    // 뚜껑으로 보였다. 물이 흐른 자리는 둘레가 아메바처럼 삐져 나오고, 표면은 가운데가
    // 넓게 부풀고 가장자리에서 급히 땅으로 말려 들어가는 물방울(비드) 모양이다.
    //   squash  가로세로 비. 1보다 작으면 옆으로 퍼진다.
    //   turn    둘레 무늬를 돌린다. 겹쳐 쓰는 조각끼리 어긋나 보이게 할 때 쓴다.
    public static Mesh Puddle(string name, float radius, float height, float squash, float turn, int sides, int rings)
    {
        Vector3 Point(int side, float t)
        {
            float angle = side / (float)sides * Mathf.PI * 2f;
            float wobble = PuddleWobble(angle, turn);

            // 가운데는 넓고 평평하게, 가장자리에서 빠르게 떨어지는 곡선.
            //
            // Cos은 t=1에서 딱 0이 아니라 아주 작은 음수(-4e-8)가 나온다. 음수를 Pow에 넣으면
            // NaN이 되고, 정점 하나가 NaN이면 메시 경계가 통째로 NaN이 되어 화면에서 사라진다.
            float r = radius * wobble * t;
            float y = height * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(t * Mathf.PI * 0.5f)), 0.45f);

            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r * squash);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        Vector3 top = new Vector3(0f, height, 0f);

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            // 수면
            for (int ring = 0; ring < rings; ring++)
            {
                float t0 = ring / (float)rings;
                float t1 = (ring + 1) / (float)rings;

                if (ring == 0)
                {
                    // 감는 방향이 옆의 사각형과 같아야 한다. 거꾸로 감으면 수면 가운데만
                    // 아래를 보게 되어 웅덩이 한복판에 검은 얼룩이 앉는다.
                    AddTriangle(vertices, triangles, top, Point(next, t1), Point(i, t1));
                    continue;
                }

                AddQuad(vertices, triangles, Point(i, t0), Point(next, t0), Point(next, t1), Point(i, t1));
            }

            // 바닥. 물이 닿은 자리를 그대로 덮는다. 아래를 보게 감는다.
            AddTriangle(vertices, triangles, Vector3.zero, Point(i, 1f), Point(next, 1f));
        }

        Mesh mesh = Build(name, vertices, triangles);

        // 물은 각져 보이면 안 된다. 같은 자리에 있는 정점들의 법선을 하나로 모아 매끈하게 만든다.
        Smooth(mesh);
        return mesh;
    }

    // ───────────────────────── 만드는 데 쓰는 것들 ─────────────────────────

    // 면마다 따로 두었던 정점의 법선을 평균 내어 매끈한 곡면으로 보이게 한다.
    //
    // Build은 저폴리 느낌을 살리려고 면마다 정점을 따로 둔다(각진 그림자). 물처럼 매끈해야
    // 하는 것에는 그것이 어울리지 않아서, 만든 뒤에 같은 자리 정점을 묶어 법선을 고쳐 준다.
    private static void Smooth(Mesh mesh)
    {
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;

        Dictionary<Vector3Int, Vector3> sums = new Dictionary<Vector3Int, Vector3>();

        Vector3Int Key(Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3Int key = Key(positions[i]);
            sums[key] = sums.TryGetValue(key, out Vector3 sum) ? sum + normals[i] : normals[i];
        }

        for (int i = 0; i < positions.Length; i++)
        {
            normals[i] = sums[Key(positions[i])].normalized;
        }

        mesh.normals = normals;
    }

    // 면이 바깥을 보게 맞춘다.
    //
    // 삼각형을 어느 쪽으로 감아야 앞면이 되는지는 축을 어떻게 잡았느냐에 따라 뒤집힌다.
    // 손으로 맞추면 도형 하나를 고칠 때마다 다른 하나가 뒤집혀서, 산 한쪽 면만 새까맣게
    // 나오는 것을 눈으로 찾아야 했다. 그래서 다 만든 뒤에 한 번 재서 필요하면 통째로 뒤집는다.
    //
    // 재는 법: 면마다 "중심에서 면으로 가는 방향"과 면의 법선을 견준다.
    // 속이 찬 도형이라면 바깥을 보는 면이 압도적으로 많다.
    private static void FaceOutward(List<Vector3> vertices, List<int> triangles)
    {
        Vector3 center = Vector3.zero;
        foreach (Vector3 vertex in vertices)
        {
            center += vertex;
        }

        center /= Mathf.Max(1, vertices.Count);

        float score = 0f;

        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];

            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 outward = (a + b + c) / 3f - center;

            score += Vector3.Dot(normal, outward);
        }

        if (score >= 0f)
        {
            return;
        }

        for (int i = 0; i < triangles.Count; i += 3)
        {
            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTriangle(vertices, triangles, a, b, c);
        AddTriangle(vertices, triangles, a, c, d);
    }

    // 면마다 정점을 따로 두었으므로 법선을 다시 계산하면 각진 저폴리 느낌이 그대로 산다.
    private static Mesh Build(string name, List<Vector3> vertices, List<int> triangles)
    {
        FaceOutward(vertices, triangles);

        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
