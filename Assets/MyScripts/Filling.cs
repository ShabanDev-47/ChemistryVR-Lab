using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Filling : MonoBehaviour
{
    public Renderer _TestRenderer;    
    private float _renderValue;
    public ParticleSystem _AmoniaSmoke;
    public GameObject _PotassiumFX;
    public GameObject Potassium;
    

    private AudioSource _audio;
    public AudioClip audioClip;

    private void Start()
    {
        _audio = GetComponent<AudioSource>();
       // _AmoniaSmoke.Pause();
        _AmoniaSmoke.Stop();
    }
    //Detecting what chemical element hit the Container:
    private void OnParticleCollision(GameObject other)
    {       
        this.gameObject.tag = other.gameObject.tag;
        _renderValue += 1 / 100f;
        _TestRenderer.material.SetFloat("_FillAmount", _renderValue);                                             
    }
    private void OnTriggerEnter(Collider other)
    {
        if (this.gameObject.tag =="HCL" && other.gameObject.tag == "Amonia")
        {
            //making K false, SO the player can't do both equation @ Once.
            Potassium.SetActive(false);
            _AmoniaSmoke?.Play();
        }

        if (this.gameObject.tag =="Amonia" && other.gameObject.tag == "HCL")
        {
            _AmoniaSmoke?.Play();
        }


        if(this.gameObject.tag=="Water" && other.gameObject.tag == "K")
        {
            //Play Audio:
            _audio.clip = audioClip;
            _audio.Play();

            Destroy(other.gameObject);
            StartCoroutine(PotassiumEffect());
        }

    }

    IEnumerator PotassiumEffect()
    {
        yield return new WaitForSeconds(2.23f);
        _PotassiumFX.SetActive(true);
        //Hide The container so the player press "RESET":

    }
    
    public void ResetContainer()
    {
        Potassium.SetActive(true);
        _renderValue = 0;
        _TestRenderer.material.SetFloat("_FillAmount", _renderValue);
        this.gameObject.tag = "Untagged";     
        
        _AmoniaSmoke?.Stop();
        _PotassiumFX?.SetActive(false);

    }



}
