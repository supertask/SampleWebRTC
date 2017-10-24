/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System;
using Byn.Net;
using System.Collections.Generic;
using Byn.Common;

/// <summary>
/// Contains a complete chat example.
/// It can run on Windows x86/x64 and in browsers. More platforms will be added soon.
/// 
/// The chat app will report during start which system it uses.
/// 
/// The user can enter a room name and click the "Open room" button to start a server and wait for
/// incoming connections or use the "Join room" button to join an already existing room.
/// 
/// 
/// 
/// 
/// As the system implements a server/client style connection all messages will first be sent to the
/// server and the server delivers it to each client. The server side ConnectionId is used to
/// identify a user.
/// 
/// 
/// </summary>
public class RealSocket
{
	//But I don't know 


    /// <summary>
    /// This is a test server. Don't use in production! The server code is in a zip file in WebRtcNetwork
    /// </summary>
	public string serverIpAddress;
	public string signalingServerUrl;
	public string websocketUrl;

    //public string uIceServer = "stun:because-why-not.com:12779";
	public string uIceServer = "stun:stun.l.google.com:19302";
    public string uIceServerUser = "";
    public string uIceServerPassword = "";

    /// <summary>
    /// Mozilla stun server. Used to get trough the firewall and establish direct connections.
    /// Replace this with your own production server as well. 
    /// </summary>
  	public string uIceServer2 = "stun:stun.services.mozilla.com:3478";
  	//public string uIceServer2 = "stun:localhost:19302";

    /// <summary>
    /// The network interface.
    /// This can be native webrtc or the browser webrtc version.
    /// (Can also be the old or new unity network but this isn't part of this package)
    /// </summary>
    private IBasicNetwork mNetwork = null;

	private string selfNodeID = "";
	private string selfConnectionID = "";
	private List<string> neighborAddresses = new List<string>();
    private List<ConnectionId> mConnections = new List<ConnectionId>();
	private WebSocket ws;


	private string SETTING_NODE = "SETTING_NODE@";

	public RealSocket() {
		this.serverIpAddress = "192.168.1.7";
		this.signalingServerUrl = "wss://" + this.serverIpAddress + ":12777/conferenceapp"; //wssでないとだめ
		this.websocketUrl = "ws://" + this.serverIpAddress + ":18080/";
		this.ws = new WebSocket(new Uri(this.websocketUrl));
    }

    private void Setup() {
		mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(signalingServerUrl, new IceServer[] { new IceServer(uIceServer, uIceServerUser, uIceServerPassword), new IceServer(uIceServer2) });
        if (mNetwork == null)
			Debug.Log("Failed to access webrtc!!!!");
    }

    private void Reset() {
        mConnections = new List<ConnectionId>();
        this.mNetwork.Dispose();
        this.mNetwork = null;
    }


    void OnDestroy() {
		if (mNetwork != null) this.Reset();
    }

	/*
	 * 必ず一定の感覚で呼ばれる．
	 */
    public void FixedUpdate() {
        this.ReceiveNetwork();
    }
	private void ReceiveNetwork()
    {
        //check if the network was created
		if (mNetwork == null) { return; }

        //first update it to read the data from the underlaying network system
        mNetwork.Update();

        //handle all new events that happened since the last update
        NetworkEvent evt;
        //check for new messages and keep checking if mNetwork is available. it might get destroyed
        //due to an event
        while (mNetwork != null && mNetwork.Dequeue(out evt))
        {
            //print to the console for debugging
            //Debug.Log(evt);

            //check every message
            switch (evt.Type) {
                case NetEventType.ServerInitialized:
                    string address = evt.Info; //addressは仮想アドレス（ルーム名）
					Debug.Log("Server started. Address: " + address);
                    break;
                case NetEventType.ServerInitFailed:
					Debug.Log("Server start failed.");
                    Reset();
                    break;
                case NetEventType.ServerClosed:
                    //server shut down. reaction to "Shutdown" call or
                    //StopServer or the connection broke down
					Debug.Log("Server closed.");
                    break;
                case NetEventType.NewConnection:
                    //mConnections.Add(evt.ConnectionId);
                    //either user runs a client and connected to a server or the
                    //user runs the server and a new client connected
					//Debug.Log("New local connection! ID: " + evt.ConnectionId);

                    //user runs a server. announce to everyone the new connection
                    //using the server side connection id as identification
                    string msg = "New connectionId " + evt.ConnectionId + " joined the room.";
					Debug.Log(msg);
					this.BroadcastToNeighbors(msg);
                    break;
                case NetEventType.ConnectionFailed:
                    //Outgoing connection failed. Inform the user.
					//Debug.Log("Connection failed");
                    Reset();
                    break;
                case NetEventType.Disconnected:
                    //mConnections.Remove(evt.ConnectionId);
                    //A connection was disconnected
                    //If this was the client then he was disconnected from the server
                    //if it was the server this just means that one of the clients left
					//Debug.Log("Local Connection ID " + evt.ConnectionId + " disconnected");
                    string userLeftMsg = "User " + evt.ConnectionId + " left the room.";

                    //show the server the message
					Debug.Log(userLeftMsg);

                    //other users left? inform them 
                    if (mConnections.Count > 0) {
						BroadcastToNeighbors(userLeftMsg);
                    }
                    break;
                case NetEventType.ReliableMessageReceived:
                case NetEventType.UnreliableMessageReceived:
                    ReceiveIncommingMessage(ref evt);
                    break;
				default:
					break;
            }
        }

        //finish this update by flushing the messages out if the network wasn't destroyed during update
        if(mNetwork != null)
            mNetwork.Flush();
    }

	/*
	 * メッセージを受信する．
	 */
	private void ReceiveIncommingMessage(ref NetworkEvent evt) {
        MessageDataBuffer buffer = (MessageDataBuffer)evt.MessageData;
        string msg = Encoding.UTF8.GetString(buffer.Buffer, 0, buffer.ContentLength);
		Debug.Log ("msg: " + msg);

		if (msg.IndexOf(this.SETTING_NODE) == 0) {
			Debug.Log ("GGGGGGGGGGGGGGGGGeeeeet received: " + msg);
			string newAddress = msg.Remove (0, this.SETTING_NODE.Length).Trim();
			this.mConnections.Add(evt.ConnectionId);
			ConnectionId cid = mNetwork.Connect(newAddress);
			Debug.Assert (cid == evt.ConnectionId);
		}
        //return the buffer so the network can reuse it
        buffer.Dispose();
    }

	public bool checkConnection() {
		if (mNetwork == null || mConnections.Count == 0) {
			Debug.Log ("No connection. Can't send message.");
			return false;
		}
		else { return true; }
	}

	/*
	 * 他のノードに指定したメッセージをブロードキャストする．
	 */
	public void BroadcastToNeighbors(string msg, bool reliable = true) {
		if (this.checkConnection()) {
			byte[] msgData = Encoding.UTF8.GetBytes (msg);
			foreach (ConnectionId id in mConnections) {
				mNetwork.SendData (id, msgData, 0, msgData.Length, reliable);
			}
		}
	}
	public void SendString(ConnectionId cid, string msg, bool reliable = true) {
		if (this.checkConnection()) {
			byte[] msgData = Encoding.UTF8.GetBytes (msg);
			mNetwork.SendData (cid, msgData, 0, msgData.Length, reliable);
		}
	}

	private void ConnectToWebRTC() {
		//WebRtcNetworkFactory factory = WebRtcNetworkFactory.Instance;
		if (mNetwork == null) this.Setup();
		mNetwork.StartServer(selfNodeID.ToString());

		foreach(string address in this.neighborAddresses) {
			Debug.Log ("Connect to address," + address);
			ConnectionId cid = mNetwork.Connect(address);
			mConnections.Add(cid);
			//Debug.Log("Connecting to ADDRESS=" + address + ", cid=" + cid.id + " ...");
			Debug.Log("Hereeeeeeeeeeeeeeeeeeee: Send SETTING_NODE to address," + address);
			this.SendString(cid, this.SETTING_NODE + address);
		}
	}


	//
	//
	// Setting a signaling server(Websocket).
	// ========================================================================
	//

	public IEnumerator Connect() {
		yield return this.ws.Connect ();
	}

	public IEnumerator Listen() {
		this.ws.SendString("J");
		while (true) {
			string msg = this.ws.RecvString ();
			if (msg != null) {
				string[] nodeStrs = msg.Split (',');
				neighborAddresses = new List<string>(nodeStrs);
				int lastIndex = neighborAddresses.Count - 1;
				this.selfNodeID = neighborAddresses[lastIndex];
				neighborAddresses.RemoveAt(lastIndex);
				break;
			}
			if (this.ws.error != null) {
				Debug.LogError ("Error: " + this.ws.error);
				break;
			}
			yield return 0;
		}
		this.ConnectToWebRTC();
	}


	public void Close() {
		string leaveSignal = "L" + this.selfNodeID;
		Debug.Log(leaveSignal);
		this.ws.SendString(leaveSignal);
		this.ws.Close();
	}
}
