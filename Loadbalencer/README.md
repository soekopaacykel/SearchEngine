# Load Balancer

This is a simple HTTP load balancer that distributes requests randomly between multiple SearchAPI instances.

## Configuration

The load balancer is configured to forward requests to these backend servers:

- http://localhost:5154 (SearchAPI Instance 1)
- http://localhost:5155 (SearchAPI Instance 2)

## Usage

1. **Start the SearchAPI instances:**

   ```bash
   # Terminal 1 - Start first instance
   cd SearchAPI
   dotnet run --launch-profile http

   # Terminal 2 - Start second instance
   cd SearchAPI
   dotnet run --launch-profile http-instance2
   ```

2. **Start the Load Balancer:**

   ```bash
   # Terminal 3 - Start load balancer
   cd Loadbalencer
   dotnet run
   ```

3. **Access your application through the load balancer:**
   - Load Balancer URL: http://localhost:5000
   - All requests to port 5000 will be randomly distributed between the two SearchAPI instances

## Features

- **Random Load Balancing**: Requests are distributed randomly between backend servers
- **Asynchronous**: All operations are async for better performance
- **Request Forwarding**: Forwards all HTTP methods (GET, POST, PUT, DELETE, etc.)
- **Header Preservation**: Maintains request and response headers
- **Body Forwarding**: Supports request bodies for POST/PUT operations
- **Error Handling**: Returns 502 Bad Gateway if backend servers are unreachable
- **Logging**: Console logging shows which backend server handled each request

## Ports

- Load Balancer: **5000**
- SearchAPI Instance 1: **5154**
- SearchAPI Instance 2: **5155**
