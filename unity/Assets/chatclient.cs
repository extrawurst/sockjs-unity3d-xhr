using System.Net.Sockets;
using UnityEngine;

public class chatclient : MonoBehaviour {

	public string host;
	public GUISkin skin;
	
	private string m_input = "";
	private string m_chat = "";
	private string m_username = "username";
	private Vector2 m_scrollPos;
	private readonly SockjsClient m_sockjs = new SockjsClient();

	// Use this for initialization
	public void Start()
	{
		// will sent empty msg to meisure latency regularly (disabled by default)
		m_sockjs.AutoPingRefreshMs = 2000;

		m_sockjs.OnMessage += OnMessage;
		m_sockjs.OnConnect += OnConnect;
	}

	public void Update()
	{
		m_sockjs.Update();
	}

	private void OnMessage(string _msg)
	{
		if (string.IsNullOrEmpty(_msg))
			return;

		m_chat += _msg+'\n';

		// scroll down
		m_scrollPos = new Vector2(0,float.PositiveInfinity);
	}

	private void OnConnect()
	{
		m_sockjs.SendData(string.Format("<b>{0}:</b> joined",m_username));
	}

	public void OnApplicationQuit()
	{
		SendLeaveMessage();
	}

	public void OnApplicationPause(bool _pauseStatus)
	{
		if (m_sockjs != null)
			m_sockjs.SendData(string.Format("<b>{0}:</b> {1}", m_username, _pauseStatus ? "is away" : "is online"),true);
	}

	private void SendLeaveMessage()
	{
		m_sockjs.SendData(string.Format("<b>{0}:</b> leaves", m_username), true);
	}

	public void OnGUI ()
	{
		GUI.skin = skin;

		if(m_sockjs.State == SockjsClient.ConnectionState.Disconnected)
		{
			if(GUI.Button(new Rect(0,0,100,60),"connect"))
			{
				m_sockjs.Connect(host);
			}

			m_username = GUI.TextField(new Rect(100, 0, 200, 60), m_username);
		}
		else if (m_sockjs.State == SockjsClient.ConnectionState.Connecting)
		{
			GUI.Label(new Rect(10, 10, 400, 100), "connecting to: "+m_sockjs.Host);
		}
		else if(m_sockjs.State == SockjsClient.ConnectionState.Connected)
		{
			GUI.Label(new Rect(Screen.width - 80,0,80,25), "ping: "+m_sockjs.Ping);

			if(GUI.Button(new Rect(0,0,100,60),"disconnect"))
			{
				SendLeaveMessage();
				m_sockjs.Disconnect();
			}
			
			if(GUI.Button(new Rect(100,0,100,60),"send"))
			{
				m_sockjs.SendData(string.Format("<b>{0}:</b> {1}", m_username, m_input));
			}
			
			m_input = GUI.TextField(new Rect(200,00,Screen.width - (100+200),60), m_input);

			var textAreaWidth = Screen.width;
			var textAreaHeight = Screen.height - 60;

			// scrollable textarea
			GUILayout.BeginArea(new Rect(0,60,textAreaWidth,textAreaHeight));
			{
				m_scrollPos = GUILayout.BeginScrollView(m_scrollPos, false, true,
					GUILayout.Width(textAreaWidth),
					GUILayout.Height(textAreaHeight));
				{
					GUILayout.Box(m_chat);
				}
				GUILayout.EndScrollView();
			}
			GUILayout.EndArea();
		}
	}
}
