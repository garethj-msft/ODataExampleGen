@echo off
    REM Creating a Newline variable (the two blank lines are required!)FileSource
set NLM=^


set NL=^^^%NLM%%NLM%^%NLM%%NLM%
echo Generating examples for Microsoft Graph 

:: ---------------------- mailboxes ----------------------------
echo %NL%---------------------- Examples for me/mailFolders ----------------------------
%~dp0\..\bin\debug\netcoreapp3.1\ODataExampleGen -m GET -c %~dp0\GraphBetaMetaData.csdl -u me/mailFolders/AAMkAGVmMDEzMTM4LTZmYWUtNDdkNC1hMDZiLTU1OGY5OTZhYmY4OAAuAAAAAAAiQ8W967B7TKBjgx9rVEURAQAiIsqMbYjsS5e -r displayName:"SavedForLater" childFolderCount:2 totalItemCount:1024 unreadItemCount:812 -p mailFolders:mailFolder

