import subprocess
import os
import scp
import sys
import argparse
import glob
import ast

from paramiko import SSHClient
from paramiko import sftp
from paramiko import sftp_client
from scp import SCPClient
from tkinter import filedialog
from tkinter import simpledialog
from tkinter import Tk
from tkinter.filedialog import askopenfilename


parser = argparse.ArgumentParser(description='This script can be use to launch, list or kill Unity standalone builds available on the cluster ')
parser.add_argument('-s', '--startCluster', type=int, help='')
parser.add_argument('-l', '--list', action="store_true", help='')
parser.add_argument('-k', '--kill', help='')
args = parser.parse_args()


def getExe():
    ssh = SSHClient()
    ssh.load_system_host_keys()
    ssh.connect(hostname='10.1.32.130', username='unity')

    cmd = """python -c "import glob; print(glob.glob('C:/Cluster_Rendering/*/*.exe'))\""""
    stdin, stdout, stderr = ssh.exec_command(cmd)
    remExe = ast.literal_eval(str(stdout.read().decode(encoding="UTF-8").strip()))

    pathToExe = []
    for path in remExe:
        if not path.endswith("UnityCrashHandler64.exe") and not path.endswith("UnityCrashHandler32.exe"):
            pathToExe.append(path)
    return pathToExe


if args.list:
    for (i, item) in enumerate(getExe(), start=0):
        print("[{}] {}".format(i, item))

    sys.exit()

if args.startCluster:
    path = getExe()[args.startCluster]

    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Master -i -s ' + path + ' -useClusterRendering -masterNode 0 3 224.0.1.0:25689,25690 30 -logFile C:\\Cluster_Rendering\\clusterLog.txt')
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node1 -i -s ' + path +  ' -useClusterRendering -node 1 224.0.1.0:25690,25689 30 -logFile C:\\Cluster_Rendering\\clusterLog.txt')
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node2 -i -s ' + path +  ' -useClusterRendering -node 2 224.0.1.0:25690,25689 30 -logFile C:\\Cluster_Rendering\\clusterLog.txt')
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node3 -i -s ' + path +  ' -useClusterRendering -node 3 224.0.1.0:25690,25689 30 -logFile C:\\Cluster_Rendering\\clusterLog.txt')

if args.kill:
    process = getExe()[args.kill].split('\\')[-1]
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Master taskkill /IM "{}" /f'.format(process))
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node1 taskkill /IM "{}" /f'.format(process))
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node2 taskkill /IM "{}" /f'.format(process))
    subprocess.Popen('c:\\PSTools\\PsExec.exe \\\\YUL-CR-Node3 taskkill /IM "{}" /f'.format(process))