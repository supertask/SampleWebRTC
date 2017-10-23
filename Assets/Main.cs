using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour {

	RealSocket socket;

	// Use this for initialization
	public IEnumerator Start () {
		this.socket = new RealSocket();
		yield return StartCoroutine (this.socket.Connect());
		yield return this.socket.Listen();
	}
	
	// Update is called once per frame
	//public void Update () {
	//}

    public void FixedUpdate() {
		this.socket.FixedUpdate();
	}

	public void OnApplicationQuit() {
		this.socket.Close();
	}
}
