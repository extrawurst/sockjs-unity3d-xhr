var http = require('http');
var sockjs = require('sockjs');

// 1. sockjs server
var sockjs_opts = {
	websocket: false
	//heartbeat_delay: 25000
	//disconnect_delay: 5000
	//log: function(severity,msg)
	};

var clients = {};
	
var sockjs_echo = sockjs.createServer(sockjs_opts);

sockjs_echo.on('connection', function(conn) {
	
	console.log('new connection: '+conn.id);
	
	clients[conn.id] = conn;
	
    conn.on('data', function(message) {

		for(client in clients)
			clients[client].write(message);
    });
	
	conn.on('close', function(message) {
        console.log('disconnect: '+conn.id);
		
		delete clients[conn.id];
    });
});

// 3. Usual http stuff
var server = http.createServer();

sockjs_echo.installHandlers(server, {prefix:'/echo'});

console.log(' [*] Listening on 0.0.0.0:9999' );
server.listen(9999, '0.0.0.0');
