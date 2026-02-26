#!/bin/bash
# Azure CLI Authentication Helper for Docker Container
# This script helps set up Azure CLI authentication for the migration container

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🔐 Azure CLI Authentication Setup for Docker Container${NC}"
echo "========================================================"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}❌ Docker is not running. Please start Docker and try again.${NC}"
    exit 1
fi

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Azure CLI is not installed on the host system.${NC}"
    echo -e "${YELLOW}💡 Install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli${NC}"
    exit 1
else
    echo -e "${GREEN}✅ Azure CLI is installed on host${NC}"
fi

# Check if already authenticated
if az account show > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Already authenticated to Azure CLI${NC}"
    echo ""
    echo -e "${BLUE}📋 Current account info:${NC}"
    az account show --output table
else
    echo -e "${YELLOW}⚠️  Not authenticated to Azure CLI${NC}"
    echo -e "${BLUE}🔑 Starting Azure CLI login process...${NC}"
    echo ""
    az login
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Successfully authenticated to Azure CLI${NC}"
        echo ""
        echo -e "${BLUE}📋 Current account info:${NC}"
        az account show --output table
    else
        echo -e "${RED}❌ Azure CLI login failed${NC}"
        exit 1
    fi
fi

echo ""
echo -e "${BLUE}🔨 Building Docker image with Azure CLI support...${NC}"
docker build -t v1-to-v2-migration .

echo ""
echo -e "${BLUE}🧪 Testing Azure CLI authentication in container...${NC}"
docker run --rm -it \
    --network host \
    -v ~/.azure:/home/migration/.azure \
    v1-to-v2-migration \
    /bin/bash -c "az account show --output table || echo 'Authentication test failed'"

echo ""
echo -e "${GREEN}✅ Setup complete! You can now run migration commands.${NC}"
echo ""
echo -e "${YELLOW}📋 Example commands:${NC}"
echo -e "${NC}   ./run-migration.sh --help${NC}"
echo -e "${NC}   ./run-migration.sh --project-endpoint \"https://your-endpoint\" --use-v2-api your-assistant-id${NC}"
echo -e "${NC}   ./run-migration.sh --project-connection-string \"your-connection-string\" --use-v2-api your-assistant-id${NC}"
echo ""