# To use this script you must install Python 3.X and install the SCP, Paramiko modules. PSExec must be installed in c:/PSTools

import subprocess
import os
import scp
import sys
import argparse
import glob
import ast
import json

from paramiko import SSHClient
from paramiko import sftp
from paramiko import sftp_client
from scp import SCPClient

def getExe(config):

    ssh = SSHClient()
    ssh.load_system_host_keys()
    ssh.connect(hostname=config['servers'][0]['name'], username=config['username'])
    cmd = """python -c "import glob; print(glob.glob('"""+ config['base_destination']+"""**/*.exe', recursive=True))\""""
    stdin, stdout, stderr = ssh.exec_command(cmd)
    remExe = ast.literal_eval(str(stdout.read().decode(encoding="UTF-8").strip()))

    pathToExe = []
    for path in remExe:
        if not path.endswith("UnityCrashHandler64.exe") and not path.endswith("UnityCrashHandler32.exe"):
            pathToExe.append(path)
    return pathToExe

def main():
    parser = argparse.ArgumentParser(description='This script can be use to list, launch or kill Unity standalone builds available on the cluster.')
    parser.add_argument('-l', '--list', action="store_true", help='This option will list all available standalone build in the base_directory specified in the Config.json file.')
    parser.add_argument('-s', '--startCluster', type=int, help='This option will launch the Cluster Display with the Unity build specified by the list number.')
    parser.add_argument('-k', '--kill', type=int, help='This option will kill the Unity build specified by the list number.')
    if len(sys.argv)==1:
        parser.print_help(sys.stderr)
        sys.exit(1)

    args = parser.parse_args()

    with open('Config.json') as json_file:
        config = json.load(json_file)




    if args.list:
        print("Listing...")
        for (i, item) in enumerate(getExe(config), start=0):
            print("[{}] {}".format(i, item))

        sys.exit()

    if args.startCluster is not None:
        print("Starting...")
        path = getExe(config)[args.startCluster]

        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][0]['name'] + ' -i -s ' + path + ' -logFile c:/Cluster_Rendering/log0.txt -handshakeTimeout 30000 -communicationTimeout 5000 -masterNode 0 3 224.0.1.0:25689,25690')
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][1]['name'] + ' -i -s ' + path + ' -logFile c:/Cluster_Rendering/log1.txt -handshakeTimeout 30000 -communicationTimeout 6000 -node 1 224.0.1.0:25690,25689')
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][2]['name'] + ' -i -s ' + path + ' -logFile c:/Cluster_Rendering/log2.txt -handshakeTimeout 30000 -communicationTimeout 6000 -node 2 224.0.1.0:25690,25689')
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][3]['name'] + ' -i -s ' + path + ' -logFile c:/Cluster_Rendering/log3.txt -handshakeTimeout 30000 -communicationTimeout 6000  -node 3 224.0.1.0:25690,25689')

    if args.kill is not None:
        print("Killing...")
        process = getExe(config)[args.kill].split('\\')[-1]

        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][0]['name'] + ' taskkill /IM "{}" /f'.format(process))
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][1]['name'] + ' taskkill /IM "{}" /f'.format(process))
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][2]['name'] + ' taskkill /IM "{}" /f'.format(process))
        subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\' + config['servers'][3]['name'] + ' taskkill /IM "{}" /f'.format(process))

if __name__ == "__main__":
    main()