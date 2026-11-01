using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Qualitysettings : MonoBehaviour
{
    public UnityEngine.Rendering.VolumeProfile environmentProfile;

    void Start()
    {
        SetQuality();
    }

    public void SetQuality()
    {
        if (environmentProfile && environmentProfile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
            bloom.active = QualitySettings.GetQualityLevel() == 0;
    }
}
