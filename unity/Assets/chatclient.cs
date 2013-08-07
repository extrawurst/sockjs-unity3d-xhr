using UnityEngine;
using System.Collections;

public class chatclient : MonoBehaviour {

	public string host;
	public SockjsClient sockjs;
	
	private string m_input = "";
	private string m_chat = "";

	// Use this for initialization
	void Start () {
		sockjs.OnMessage += OnMessage;
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	
	void OnMessage(string _msg)
	{
		m_chat += '\n' + _msg;
	}
	
	void OnGUI () {
		
		if(sockjs.State == SockjsClient.ConnectionState.disconnected)
		{
			if(GUI.Button(new Rect(0,0,200,30),"connect"))
			{
				sockjs.Connect(host);
			}
		}
		else if(sockjs.State == SockjsClient.ConnectionState.connected)
		{
			if(GUI.Button(new Rect(0,0,200,30),"disconnect"))
			{
				sockjs.Disconnect();
			}
			
			if(GUI.Button(new Rect(0,30,200,30),"send"))
			{
				sockjs.SendData(m_input);
			}
			
			m_input = GUI.TextField(new Rect(200,30,200,30), m_input);
			
			GUI.TextArea(new Rect(0,60,400,100), m_chat);
		}
	}
}
