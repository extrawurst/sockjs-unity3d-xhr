[![Analytics](https://ga-beacon.appspot.com/UA-49903757-1/sockjs-unity3d-xhr/readme)](https://github.com/Extrawurst/sockjs-unity3d-xhr)
sockjs-unity3d-xhr
==================

sockjs client implementation for unity3D using xhr polling (so this works with unity free license).

==================
Usage
==================

Simple usage example:

```
public class Client : MonoBehaviour {

	public SockjsClient sockjs = new SockjsClient();
	
	public void Start()
	{
		sockjs.OnMessage += OnMessage;
		sockjs.OnConnect += OnConnect;
		
		// connect to wherever your server is running
		sockjs.Connect("http://localhost:9999/echo/");
	}

	private void OnMessage(string _msg)
	{
		// got message
		Debug.Log(_msg);
	}

	private void OnConnect()
	{
		Debug.Log("connected");
		
		sockjs.SendData("hello world");
	}
}
```

For an extensive example see the example unity project at https://github.com/Extrawurst/sockjs-unity3d-xhr-example
