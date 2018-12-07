Param([string]$FirewallRuleName, [int]$Port)

if ($FirewallRuleName -eq "" -or $Port -eq 0)
{
    echo "Usage: Setup.ps1 -FirewallRuleName <RuleName> -Port <Port>"
    echo "";
    exit;
}

echo "Adding firewall rules"
netsh advfirewall firewall add rule name=$FirewallRuleName dir=in action=allow program=CTHost.exe profile=any localport=$Port protocol=tcp

echo "Done"