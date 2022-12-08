using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleRadialFFController : MonoBehaviour
{
    public Transform ffTransform;
    public float radius = 1.0f;
    public float frequency = 1.0f;
    public bool ccw = true;
    public float offset = 0.0f;

    void Start()
    {
        if (!ffTransform) ffTransform = this.transform;
    }

    void Update()
    {
        Vector3 lPos = ffTransform.localPosition;
        lPos.x = radius * Mathf.Cos(frequency * Time.time * (ccw ? 1.0f : -1.0f) + offset);
        lPos.y = radius * Mathf.Sin(frequency * Time.time * (ccw ? 1.0f : -1.0f) + offset);
        ffTransform.localPosition = lPos;
    }
}