#/bin/bash

dotnet restore
dotnet build --no-restore

# Run the 2 console apps in different windows

dotnet run --project ./src/Adventure.SiloHost --no-build & 
sleep 10
#dotnet run --project ./src/Adventure.Client --no-build &
dotnet run --project ./src/Adventure.SocketClient --no-build &