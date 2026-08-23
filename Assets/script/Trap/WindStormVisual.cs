using UnityEngine;

// 바람(Wind) 원소가 쓰는 흰 폭풍을 만든다.
//
// 새 연출을 따로 만들지 않고, 맵에 이미 서 있는 모래바람(Sandstorm)의 연출을 통째로 베껴
// 색만 흰색으로 물들인다. "같은 폭풍인데 색만 다르다"는 것이 눈에 바로 보여야 하기 때문이다.
//
// 베낄 때 판정 콜라이더나 상쇄 칸까지 따라오면 흰 폭풍이 진짜 함정처럼 굴게 되므로,
// 연출이 아닌 것은 전부 떼어 낸다.
public static class WindStormVisual
{
    // 모래바람 연출을 흰색으로 복제한다. 베낄 것이 없으면 null.
    // source를 비워 두면 씬에서 아무 모래바람이나 하나 찾아 쓴다.
    public static Transform CreateWhiteCopy(Sandstorm source, Vector3 position, float scale, Color tint)
    {
        if (source == null)
        {
            source = Object.FindFirstObjectByType<Sandstorm>(FindObjectsInactive.Include);
        }

        if (source == null)
        {
            Debug.LogWarning("WindStormVisual: 베낄 모래바람(Sandstorm)을 씬에서 찾지 못했습니다.");
            return null;
        }

        ParticleSystem[] particles = source.GetComponentsInChildren<ParticleSystem>(true);
        if (particles.Length == 0)
        {
            Debug.LogWarning($"{source.name}: 베낄 파티클 연출이 없습니다.", source);
            return null;
        }

        Transform sourceRoot = VisualRootOf(source.transform, particles[0].transform);

        GameObject copy = Object.Instantiate(sourceRoot.gameObject);
        copy.name = "WindStorm";
        copy.SetActive(true);
        copy.transform.SetPositionAndRotation(position, sourceRoot.rotation);

        // 원본이 이미 줄여져 있으므로(모래바람은 보통 0.03배) 그 최종 크기를 기준으로 잡는다.
        copy.transform.localScale = sourceRoot.lossyScale * Mathf.Max(0.01f, scale);

        StripNonVisual(copy);
        Tint(copy, tint);
        return copy.transform;
    }

    // 파티클이 든 가지를 타고 올라가 모래바람 바로 아래 자식(= 연출 프리팹의 뿌리)을 찾는다.
    // Sandstorm 자신을 복제하면 이동·판정·상쇄 로직까지 딸려 오므로 거기서 멈춘다.
    private static Transform VisualRootOf(Transform stormRoot, Transform particle)
    {
        Transform root = particle;

        while (root != stormRoot && root.parent != null && root.parent != stormRoot)
        {
            root = root.parent;
        }

        return root;
    }

    // 흰 폭풍은 보여 주기만 한다. 죽이거나 상쇄하는 부품은 전부 떼어 낸다.
    private static void StripNonVisual(GameObject copy)
    {
        foreach (Sandstorm storm in copy.GetComponentsInChildren<Sandstorm>(true))
        {
            Object.Destroy(storm);
        }

        foreach (SandstormKillZone zone in copy.GetComponentsInChildren<SandstormKillZone>(true))
        {
            Object.Destroy(zone.gameObject);
        }

        foreach (ElementTrapCube trap in copy.GetComponentsInChildren<ElementTrapCube>(true))
        {
            Object.Destroy(trap);
        }

        foreach (TrapCounterEffect effect in copy.GetComponentsInChildren<TrapCounterEffect>(true))
        {
            Object.Destroy(effect);
        }

        foreach (Collider hitBox in copy.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(hitBox);
        }
    }

    // 모래빛을 걷어내고 흰 폭풍으로 만든다.
    //
    // 파티클 색은 텍스처에 곱해지는 값이라, 회색조 텍스처면 흰색이 그대로 나오고
    // 텍스처 자체가 모래빛이면 덜 하얘진다. 그때는 tint를 1보다 밝게(HDR) 올리면 된다.
    public static void Tint(GameObject storm, Color tint)
    {
        foreach (ParticleSystem particles in storm.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = tint;
        }

        foreach (Renderer renderer in storm.GetComponentsInChildren<Renderer>(true))
        {
            // 원본과 재질을 공유하고 있으므로, 여기서 sharedMaterial을 건드리면 원래 모래바람까지 하얘진다.
            // 이 복제본만 쓰는 재질을 따로 만들어 준다.
            Material instance = new Material(renderer.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
            WorldVisual.SetMaterialColor(instance, tint);
            renderer.sharedMaterial = instance;
        }
    }

    // 폭풍을 잦아들게 한다. 새로 뿜는 것만 멈추고, 이미 나온 입자는 자연스럽게 사그라들게 둔다.
    public static void StopEmitting(Transform storm)
    {
        if (storm == null)
        {
            return;
        }

        foreach (ParticleSystem particles in storm.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
