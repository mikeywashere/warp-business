#!/usr/bin/env bash
# install-prerequisites.sh — Install Warp Business development prerequisites
# Supports: Ubuntu/Debian (apt) and macOS (Homebrew)
set -e

# ─── Helpers ─────────────────────────────────────────────────────────────────

print_header() { echo; echo "══════════════════════════════════════════"; echo "  $1"; echo "══════════════════════════════════════════"; }
print_ok()     { echo "  ✅  $1"; }
print_skip()   { echo "  ⏭️   $1 — already installed, skipping"; }
print_info()   { echo "  ℹ️   $1"; }

INSTALLED=()
SKIPPED=()

mark_installed() { INSTALLED+=("$1"); }
mark_skipped()   { SKIPPED+=("$1"); }

command_exists() { command -v "$1" &>/dev/null; }

# ─── Detect OS ───────────────────────────────────────────────────────────────

print_header "Detecting OS"

if [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
    print_info "macOS detected"
elif [[ -f /etc/os-release ]]; then
    . /etc/os-release
    if [[ "$ID" == "ubuntu" || "$ID_LIKE" == *"debian"* || "$ID" == "debian" ]]; then
        OS="linux"
        print_info "Ubuntu/Debian Linux detected"
    else
        echo "❌  Unsupported Linux distribution: $ID"
        echo "    This script supports Ubuntu/Debian only."
        exit 1
    fi
else
    echo "❌  Unsupported OS. This script supports macOS and Ubuntu/Debian Linux."
    exit 1
fi

# ─── Optional: Node.js ───────────────────────────────────────────────────────

INSTALL_NODE=false
echo
read -rp "Install Node.js LTS? (optional, only needed for frontend tooling) [y/N] " yn
case "$yn" in
    [Yy]*) INSTALL_NODE=true ;;
    *) print_info "Skipping Node.js" ;;
esac

# ═════════════════════════════════════════════════════════════════════════════
# macOS
# ═════════════════════════════════════════════════════════════════════════════

if [[ "$OS" == "macos" ]]; then

    # ── Homebrew ─────────────────────────────────────────────────────────────
    print_header "Homebrew"
    if ! command_exists brew; then
        echo "❌  Homebrew is not installed."
        echo "    Please install it from https://brew.sh then re-run this script."
        exit 1
    fi
    print_ok "Homebrew is available"
    brew update --quiet

    # ── .NET 10 SDK ──────────────────────────────────────────────────────────
    print_header ".NET 10 SDK"
    if command_exists dotnet && dotnet --version 2>/dev/null | grep -q "^10\."; then
        print_skip ".NET 10 SDK"
        mark_skipped ".NET 10 SDK"
    else
        print_info "Installing .NET 10 SDK via Microsoft install script..."
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
        chmod +x dotnet-install.sh
        ./dotnet-install.sh --channel 10.0
        rm dotnet-install.sh
        # Add to PATH for this session
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
        print_ok ".NET 10 SDK installed"
        mark_installed ".NET 10 SDK"
        print_info "Add to your shell profile: export DOTNET_ROOT=\"\$HOME/.dotnet\" && export PATH=\"\$DOTNET_ROOT:\$DOTNET_ROOT/tools:\$PATH\""
    fi

    # ── Docker Desktop ───────────────────────────────────────────────────────
    print_header "Docker Desktop"
    if command_exists docker; then
        print_skip "Docker Desktop"
        mark_skipped "Docker Desktop"
    else
        print_info "Installing Docker Desktop..."
        brew install --cask docker
        print_ok "Docker Desktop installed"
        mark_installed "Docker Desktop"
    fi

    # ── kubectl ──────────────────────────────────────────────────────────────
    print_header "kubectl"
    if command_exists kubectl; then
        print_skip "kubectl"
        mark_skipped "kubectl"
    else
        brew install kubectl
        print_ok "kubectl installed"
        mark_installed "kubectl"
    fi

    # ── skaffold ─────────────────────────────────────────────────────────────
    print_header "skaffold"
    if command_exists skaffold; then
        print_skip "skaffold"
        mark_skipped "skaffold"
    else
        brew install skaffold
        print_ok "skaffold installed"
        mark_installed "skaffold"
    fi

    # ── GitHub CLI ───────────────────────────────────────────────────────────
    print_header "GitHub CLI (gh)"
    if command_exists gh; then
        print_skip "GitHub CLI"
        mark_skipped "GitHub CLI"
    else
        brew install gh
        print_ok "GitHub CLI installed"
        mark_installed "GitHub CLI"
    fi

    # ── Node.js LTS ──────────────────────────────────────────────────────────
    if [[ "$INSTALL_NODE" == true ]]; then
        print_header "Node.js LTS"
        if command_exists node; then
            print_skip "Node.js"
            mark_skipped "Node.js LTS"
        else
            brew install node
            print_ok "Node.js LTS installed"
            mark_installed "Node.js LTS"
        fi
    fi

fi

# ═════════════════════════════════════════════════════════════════════════════
# Linux (Ubuntu/Debian)
# ═════════════════════════════════════════════════════════════════════════════

if [[ "$OS" == "linux" ]]; then

    # ── Base dependencies ────────────────────────────────────────────────────
    print_header "Base dependencies"
    sudo apt-get update -q
    sudo apt-get install -y curl apt-transport-https ca-certificates gnupg lsb-release
    print_ok "Base dependencies ready"

    # ── .NET 10 SDK ──────────────────────────────────────────────────────────
    print_header ".NET 10 SDK"
    if command_exists dotnet && dotnet --version 2>/dev/null | grep -q "^10\."; then
        print_skip ".NET 10 SDK"
        mark_skipped ".NET 10 SDK"
    else
        print_info "Adding Microsoft APT repository..."
        curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
            | gpg --dearmor \
            | sudo tee /usr/share/keyrings/microsoft.gpg > /dev/null
        echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/microsoft.gpg] \
https://packages.microsoft.com/ubuntu/$(lsb_release -rs)/prod $(lsb_release -cs) main" \
            | sudo tee /etc/apt/sources.list.d/microsoft-prod.list > /dev/null
        sudo apt-get update -q
        sudo apt-get install -y dotnet-sdk-10.0
        print_ok ".NET 10 SDK installed"
        mark_installed ".NET 10 SDK"
    fi

    # ── Docker Engine ────────────────────────────────────────────────────────
    print_header "Docker Engine"
    if command_exists docker; then
        print_skip "Docker"
        mark_skipped "Docker Engine"
    else
        print_info "Adding Docker APT repository..."
        sudo install -m 0755 -d /etc/apt/keyrings
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
            | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
        sudo chmod a+r /etc/apt/keyrings/docker.gpg
        echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
            | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
        sudo apt-get update -q
        sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
        sudo usermod -aG docker "$USER"
        print_ok "Docker Engine installed (re-login for group membership to take effect)"
        mark_installed "Docker Engine"
    fi

    # ── kubectl ──────────────────────────────────────────────────────────────
    print_header "kubectl"
    if command_exists kubectl; then
        print_skip "kubectl"
        mark_skipped "kubectl"
    else
        print_info "Adding Kubernetes APT repository..."
        KUBE_MINOR="$(curl -fsSL https://dl.k8s.io/release/stable.txt | sed 's/v//' | cut -d. -f1-2)"
        sudo curl -fsSL "https://pkgs.k8s.io/core:/stable:/v${KUBE_MINOR}/deb/Release.key" \
            | sudo gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg
        echo "deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] \
https://pkgs.k8s.io/core:/stable:/v${KUBE_MINOR}/deb/ /" \
            | sudo tee /etc/apt/sources.list.d/kubernetes.list > /dev/null
        sudo apt-get update -q
        sudo apt-get install -y kubectl
        print_ok "kubectl installed"
        mark_installed "kubectl"
    fi

    # ── skaffold ─────────────────────────────────────────────────────────────
    print_header "skaffold"
    if command_exists skaffold; then
        print_skip "skaffold"
        mark_skipped "skaffold"
    else
        print_info "Downloading skaffold binary..."
        curl -Lo skaffold "https://storage.googleapis.com/skaffold/releases/latest/skaffold-linux-amd64"
        sudo install skaffold /usr/local/bin/
        rm skaffold
        print_ok "skaffold installed"
        mark_installed "skaffold"
    fi

    # ── GitHub CLI ───────────────────────────────────────────────────────────
    print_header "GitHub CLI (gh)"
    if command_exists gh; then
        print_skip "GitHub CLI"
        mark_skipped "GitHub CLI"
    else
        print_info "Adding GitHub CLI APT repository..."
        curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
            | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg
        sudo chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg
        echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] \
https://cli.github.com/packages stable main" \
            | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null
        sudo apt-get update -q
        sudo apt-get install -y gh
        print_ok "GitHub CLI installed"
        mark_installed "GitHub CLI"
    fi

    # ── Node.js LTS ──────────────────────────────────────────────────────────
    if [[ "$INSTALL_NODE" == true ]]; then
        print_header "Node.js LTS"
        if command_exists node; then
            print_skip "Node.js"
            mark_skipped "Node.js LTS"
        else
            print_info "Installing Node.js LTS via NodeSource..."
            curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
            sudo apt-get install -y nodejs
            print_ok "Node.js LTS installed"
            mark_installed "Node.js LTS"
        fi
    fi

fi

# ─── .NET Aspire workload ────────────────────────────────────────────────────

print_header ".NET Aspire workload"
print_info "Running: dotnet workload install aspire"
dotnet workload install aspire
print_ok ".NET Aspire workload installed"
mark_installed ".NET Aspire workload"

# ─── Summary ─────────────────────────────────────────────────────────────────

print_header "Installation Summary"

if [[ ${#INSTALLED[@]} -gt 0 ]]; then
    echo "  Installed:"
    for item in "${INSTALLED[@]}"; do echo "    ✅  $item"; done
fi

if [[ ${#SKIPPED[@]} -gt 0 ]]; then
    echo "  Already present (skipped):"
    for item in "${SKIPPED[@]}"; do echo "    ⏭️   $item"; done
fi

echo
print_info "You may need to restart your terminal for PATH changes to take effect."
print_info "Visual Studio and VS Code are not installed by this script — install them manually."
echo
echo "  Happy developing! 🚀"
echo
