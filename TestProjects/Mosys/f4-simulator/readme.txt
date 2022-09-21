Open up cmd or Powershell in the same directory as the built executable. Type *.\Simulator.exe --help* to see all possible options.

Examples:

with status:
.\Simulator.exe --address 192.168.0.10 --port 8005

same but shorter:
.\Simulator.exe -a 192.168.0.10 -p 8005

with lens data:
.\Simulator.exe -a 192.168.0.10 -p 8005 --mode ST-FOCUS-ZOOM --lens "HJ14e x4.3B 01610044 lens file[V70].json"

from a recorded f4 file:
.\Simulator.exe -a 192.168.0.10 -p 8005 --loop --f4-file-path "07-44-15-13.f4" 