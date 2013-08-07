using UnityEngine;
using System.Collections;

public class SockjsClient : MonoBehaviour {
	
	public enum ConnectionState
	{
		disconnected,
		connecting,
		connected
	}
	
	private class PollResult
	{
		public string response;
		public bool error;
	}
	
	public delegate void OnMessageCallback(string _msg);
	public delegate void OnConnectCallback();
	public delegate void OnDisconnectCallback(int _code,string _message);
	
	public event OnMessageCallback OnMessage;
	public event OnDisconnectCallback OnDisconnect;
	public event OnConnectCallback OnConnect;
	
	private float m_lastSend;
	private Hashtable m_sendHeader = new Hashtable();
	private ConnectionState m_state;
	private string m_xhr;
	
	public ConnectionState State
	{
		get { return m_state; }
	}
	
	public bool Connected
	{
		get { return m_state == ConnectionState.connected; }
	}
	
	// Use this for initialization
	void Start () {
		
		m_sendHeader["Content-Type"] = "application/xml";
	}
	
	public void Connect(string _host)
	{
		var server_id = Random.Range(0,999);
		
		var session_id_rnd = Random.Range(0,100000000);
		
		var session_id = System.DateTime.UtcNow.ToLongTimeString() + '-' + session_id_rnd;
		
		//TODO: correct hashing
		m_xhr = _host + string.Format ("{0:000}/{1}/xhr",server_id, session_id.GetHashCode());
		
		if(m_state == ConnectionState.disconnected)
		{
			m_state = ConnectionState.connecting;
			
			StartCoroutine(Polling());
		}
	}
	
	public void Disconnect()
	{
		if(m_state != ConnectionState.disconnected)
		{
			m_state = ConnectionState.disconnected;
			
			StartCoroutine(Polling());
		}
	}
	
	public void SendData(string _payload)
	{
		var www = new WWW(m_xhr+"_send", StringToByteArray(string.Format("[\"{0}\"]",_payload)), m_sendHeader);
		
		StartCoroutine(WaitforRequest(www,null));
	}
	
	private IEnumerator Polling()
	{
		var pollResult = new PollResult();
		
		while(true)
		{
			var www = new WWW(m_xhr,new byte[]{0});
			
			StartCoroutine(WaitforRequest(www,pollResult));
			
			yield return www;
			
			if(pollResult.error)
			{
				if(Connected)
				{
					OnEventDisconnect(-1,"net error");
				}
			}
			else
			{
				if(!Connected)
				{
					if(pollResult.response.Length > 0 && pollResult.response[0] == 'o')
					{
						OnEventConnected();
					}
				}
				else
				{
					if(pollResult.response.Length > 0)
					{
						if( pollResult.response[0] == 'c')
						{
							var payload = pollResult.response.Substring(2,pollResult.response.Length-4);
							
							var separatorIdx = payload.IndexOf(',');
							
							string partCode;
							string partMessage;
							
							partCode = payload.Substring(0,separatorIdx);
							partMessage = payload.Substring(separatorIdx+1,payload.Length-separatorIdx-1);
							
							OnEventDisconnect(int.Parse(partCode),partMessage.Trim('"'));
						}
						else if( pollResult.response[0] == 'h')
						{
							Debug.Log("heartbeat");
						}
						else if( pollResult.response[0] == 'a')
						{
							var payload = pollResult.response.Substring(3,pollResult.response.Length-6);
							
							if(OnMessage != null)
								OnMessage(payload);
						}
					}
				}
			}
			
			if(!Connected)
				break;
		}
	}
	
	private void OnEventConnected()
	{
		m_state = ConnectionState.connected;
		
		if(OnConnect != null)
			OnConnect();
	}
	
	private void OnEventDisconnect(int _code, string _msg)
	{
		m_state = ConnectionState.disconnected;
		
		if(OnDisconnect != null)
			OnDisconnect(_code,_msg);
	}
	
	private IEnumerator WaitforRequest(WWW _www, PollResult _result)
	{
		yield return _www;
		
		if(_result != null)
		{
			_result.error = (_www.error != null);
			_result.response = _www.text;
		}
	}
	
	private static byte[] StringToByteArray(string str)
	{
	    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
	    return enc.GetBytes(str);
	}
	
	private static string ByteArrayToString(byte[] arr)
	{
	    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
	    return enc.GetString(arr);
	}
}
