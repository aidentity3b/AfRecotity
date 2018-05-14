using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AlertMessage : MonoBehaviour {
    float bornTime;
    [SerializeField]
    Text messageText;
    public string Message
    {
        set
        {
            messageText.text = value;
        }
    }
	// Use this for initialization
	void Start () {
        bornTime = Time.realtimeSinceStartup;
	}
	
	// Update is called once per frame
	void Update () {
		if(Time.realtimeSinceStartup - bornTime > 8)
        {
            Destroy(gameObject);
        }
	}
}
