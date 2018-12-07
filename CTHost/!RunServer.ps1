$AccountName="CT"
$AccountPassword="123qweASDzxc"

if ($AccountName -eq "" -or $AccountPassword -eq "")
{
    echo "Edit script and add account name and password before using"
    echo "";
    exit;
}

$SecurePass = ConvertTo-SecureString $AccountPassword -AsPlainText -Force
$Creds = New-Object System.Management.Automation.PSCredential($AccountName, $SecurePass)

Start-Process .\CTHost.exe -Credential $Creds
