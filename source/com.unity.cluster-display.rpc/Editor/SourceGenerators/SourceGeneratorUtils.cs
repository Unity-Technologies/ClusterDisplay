using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    internal static class SourceGeneratorUtils
    {
        public static bool TryGetExistingCompilationUnit (string filePath, out CompilationUnitSyntax compilationUnit)
        {
            Microsoft.CodeAnalysis.SyntaxTree syntaxTree = null;
            compilationUnit = null;
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    var text = File.ReadAllText(filePath);
                    syntaxTree = CSharpSyntaxTree.ParseText(text);
                }
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                return false;
            }

            return syntaxTree != null && (compilationUnit = syntaxTree.GetCompilationUnitRoot()) != null;
        }

        public static bool TryWriteCompilationUnit (string filePath, CompilationUnitSyntax compilationUnit)
        {
            try
            {
                var folderPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var formattedCode = Formatter.Format(compilationUnit, new AdhocWorkspace());
                if (File.Exists(filePath))
                    File.SetAttributes(filePath, FileAttributes.Normal);

                File.WriteAllText(filePath, formattedCode.ToFullString());
                File.SetAttributes(filePath, FileAttributes.ReadOnly);
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                return false;
            }

            return true;
        }


        public static void RemoveCompilationUnit (string compilationUnitPath, string compilationUnitName)
        {
            try
            {
                if (File.Exists(compilationUnitPath))
                    File.Delete(compilationUnitPath);

                var metaFilePath = $"{compilationUnitPath}.meta";
                if (File.Exists(metaFilePath))
                    File.Delete(metaFilePath);
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                return;
            }

            CodeGenDebug.Log($"Deleted wrapper: \"{compilationUnitName}\" at file path: \"{compilationUnitPath}\".");
        }

    }
}
