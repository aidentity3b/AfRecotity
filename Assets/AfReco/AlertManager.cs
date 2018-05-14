using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlertManager : MonoBehaviour {
    [SerializeField]
    private GameObject AlertMessagePrefab;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    public void NewAlert(string message)
    {
        AlertMessage newAlert = Instantiate(AlertMessagePrefab, transform).GetComponent<AlertMessage>();
        newAlert.Message = message;
    }

}
