#!/bin/bash
# 🚀 .NET SDK Persistent Installer for Cloud Shell

DOTNET_DIR="$HOME/.dotnet"

if [ ! -d "$DOTNET_DIR" ]; then
    echo "Installing .NET SDK..."
    curl -sSL https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh -o dotnet-install.sh
    chmod +x dotnet-install.sh
    # Matches the net10.0 requirement found in your CRM.Microservice.csproj
    ./dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
    rm dotnet-install.sh
fi

# Export paths (Add these to your ~/.bashrc for persistence)
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"

# Automate persistence in .bashrc if not already there
if ! grep -q "DOTNET_ROOT" "$HOME/.bashrc"; then
    echo -e "\n# .NET SDK Paths" >> "$HOME/.bashrc"
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\"" >> "$HOME/.bashrc"
    echo "export PATH=\"\$PATH:\$DOTNET_ROOT:\$DOTNET_ROOT/tools\"" >> "$HOME/.bashrc"
    echo "✅ Added .NET paths to ~/.bashrc"
fi

dotnet --version