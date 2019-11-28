# Cluster Display Project Copy script
# This script is used to copy a folder containing a Unity project to all nodes in the Cluster Display
# To use this script you must install Python 3.X and install the Paramiko and Tkinter modules



import os
import scp
import sys
import json
from paramiko import SSHClient
from paramiko import sftp
from paramiko import sftp_client
from scp import SCPClient
from tkinter import filedialog
from tkinter import simpledialog
from tkinter import Tk
from tkinter.filedialog import askopenfilename
import argparse
import glob

parser = argparse.ArgumentParser(description='This script will copy a Unity standalone build in the desired folder on every server specified in the nodesIP.txt file. The script can be use with or without arguments. When used without arguments, the user will be prompt for source and destination.')
parser.add_argument('-s', '--source', help= 'Absolute path of the Unity standalone build to be copied.')
parser.add_argument('-d', '--destination', help= 'Absolute path where you want the Unity standalone build to be copied on every server in the nodesIP.txt file. ')

args = parser.parse_args()

if args.source:
    source = args.source
else:
    Tk().withdraw() # we don't want a full GUI, so keep the root window from appearing
    source = filedialog.askdirectory(title='Choose Source folder') # show an "Open" dialog box and return the path to the selected file
    print(source)

if args.destination:
    destination = args.destination
else:
    Tk().withdraw()
    destination = simpledialog.askstring(title="Destination", prompt="Type absolute destination path")

ssh = SSHClient()
ssh.load_system_host_keys()

with open('Config.json') as json_file:
    config = json.load(json_file)
    for server in config['servers']:

        print("Copying " + source + " to " + destination + " on: " + server)
        ssh.connect(hostname=server, username='unity')
        sftpClient = sftp_client.SFTPClient.from_transport(ssh.get_transport())
        sftpClient.mkdir(destination)
        # SCPCLient takes a paramiko transport and progress callback as its arguments.
        scp = SCPClient(ssh.get_transport())
        # Uploading the 'test' directory with its content in the
        # '/home/user/dump' remote directory
        scp.put(source + '/.', recursive=True, remote_path=destination)
        scp.close()