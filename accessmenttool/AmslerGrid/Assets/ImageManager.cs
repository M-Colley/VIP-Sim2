using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class ImageManager : MonoBehaviour
{

    [SerializeField]
    private Image m_Image;
    [SerializeField]
    private Sprite scene1;
    [SerializeField] 
    private Sprite scene2;


    // Update is called once per frame
    void Update()
    {
        CheckImage();
        CheckVisibility();
    }

    private void CheckVisibility()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            m_Image.enabled = !m_Image.enabled;
        }
    }
    private void CheckImage()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            m_Image.sprite = scene1;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            m_Image.sprite = scene2;
        }
    }
    
}
