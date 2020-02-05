# Cluster Display Project Copy script
# This script is used to copy a folder containing a Unity project to all nodes in the Cluster Display
# To use this script you must install Python 3.X and install the SCP, Paramiko modules



import os
import scp
import sys
import json
from paramiko import SSHClient
from paramiko import sftp
from paramiko import sftp_client
from scp import SCPClient
import argparse
import glob


parser = argparse.ArgumentParser(description='This script will copy a Unity standalone build in the desired folder on every server specified in the Config.json file. The script can be use with or without arguments. When used without arguments, the user will be prompt for source and destination. Both source and destination must be specified.')
parser.add_argument('-s', '--source', type=str, help= 'Absolute path of the Unity standalone build to be copied.')
parser.add_argument('-d', '--destination', type=str, help= 'Enter the name of the folder in which you want the Unity standalone build to be copied in the base_directory specified in the Config.json file')
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)
args = parser.parse_args()

source = args.source
destination = args.destination


ssh = SSHClient()
ssh.load_system_host_keys()

with open('Config.json') as json_file:
    config = json.load(json_file)
    for server in config['servers']:

        print("Copying " + source + " to " + config['base_destination'] + destination + " on: " + server['name'])
        ssh.connect(hostname=server['name'], username=config['username'])
        sftpClient = sftp_client.SFTPClient.from_transport(ssh.get_transport())


        try:
            sftpClient.chdir(config['base_destination'])
        except IOError:
            sftpClient.mkdir(config['base_destination'])
        try:
            sftpClient.chdir(destination)
        except IOError:
            sftpClient.mkdir(destination)

        # SCPCLient takes a paramiko transport and progress callback as its arguments.
        scp = SCPClient(ssh.get_transport())
        scp.put(source + '/.', recursive=True, remote_path=config['base_destination'] + destination)
        scp.close()