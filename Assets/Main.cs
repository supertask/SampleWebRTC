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
	public void Update () {
		if (Input.GetKeyDown (KeyCode.H))
			this.socket.SendString ("Hi how are you?");
		else if (Input.GetKeyDown (KeyCode.G))
			this.socket.SendString ("I'm good!");
		else if (Input.GetKeyDown (KeyCode.Y))
			this.socket.SendString ("And you?");
		else if (Input.GetKeyDown (KeyCode.A))
			this.socket.SendString("Awesome!!");
		else if (Input.GetKeyDown (KeyCode.A))
			this.socket.SendString("Pretty bad..");
	} 

    public void FixedUpdate() {
		this.socket.FixedUpdate();
	}

	public void OnApplicationQuit() {
		this.socket.Close();
	}
}
