#!/bin/bash

# Azure OpenAI Deployment Discovery Script
# Helps you find the correct deployment name to fix 404 errors

RESOURCE_NAME="rs-sm-az-openai"
API_KEY="7w39wbuKV2FS6O7C9tImlcj0sgRrlrJX4UsC4C4RMe3pELYpKMDAJQQJ99BJACYeBjFXJ3w3AAABACOGEwJe"
ENDPOINT="https://${RESOURCE_NAME}.openai.azure.com"

echo "======================================"
echo "Azure OpenAI Deployment Discovery"
echo "======================================"
echo ""
echo "Resource: $RESOURCE_NAME"
echo "Endpoint: $ENDPOINT"
echo ""

# Test if the resource is reachable
echo "Step 1: Testing resource connectivity..."
if curl -s -o /dev/null -w "%{http_code}" "${ENDPOINT}/" -H "api-key: ${API_KEY}" | grep -q "404\|401"; then
    echo "? Resource is reachable"
else
    echo "? Resource might not be accessible. Check resource name and API key."
    exit 1
fi
echo ""

# Try common deployment names
echo "Step 2: Testing common deployment names..."
COMMON_DEPLOYMENTS=(
    "gpt-4o-mini"
    "gpt-4o"
    "gpt-4"
    "gpt-4-turbo"
    "gpt-35-turbo"
    "oncallbuddy-gpt4o-mini"
    "gpt-4.1"
)

WORKING_DEPLOYMENTS=()

for deployment in "${COMMON_DEPLOYMENTS[@]}"; do
    echo -n "  Testing '$deployment'... "
    
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
        "${ENDPOINT}/openai/deployments/${deployment}/chat/completions?api-version=2024-10-01-preview" \
        -H "Content-Type: application/json" \
        -H "api-key: ${API_KEY}" \
        -d '{"messages":[{"role":"user","content":"test"}],"max_tokens":1}' 2>/dev/null)
    
    if [ "$HTTP_CODE" == "200" ]; then
        echo "? WORKS!"
        WORKING_DEPLOYMENTS+=("$deployment")
    elif [ "$HTTP_CODE" == "429" ]; then
        echo "??  Exists but rate limited"
        WORKING_DEPLOYMENTS+=("$deployment")
    else
        echo "? Not found (HTTP $HTTP_CODE)"
    fi
done

echo ""

if [ ${#WORKING_DEPLOYMENTS[@]} -gt 0 ]; then
    echo "======================================"
    echo "? Found Working Deployment(s):"
    echo "======================================"
    for dep in "${WORKING_DEPLOYMENTS[@]}"; do
        echo "  - $dep"
    done
    echo ""
    echo "Update your appsettings.Development.json:"
    echo ""
    echo "{"
    echo "  \"AzureOpenAI\": {"
    echo "    \"Endpoint\": \"${ENDPOINT}/\","
    echo "    \"Deployment\": \"${WORKING_DEPLOYMENTS[0]}\","
    echo "    \"ApiKey\": \"${API_KEY}\","
    echo "    \"Temperature\": \"0\","
    echo "    \"MaxOutputTokens\": \"2000\""
    echo "  }"
    echo "}"
else
    echo "======================================"
    echo "? No Working Deployments Found"
    echo "======================================"
    echo ""
    echo "Possible reasons:"
    echo "  1. No deployments exist in this resource"
    echo "  2. Deployment names are non-standard"
    echo "  3. API key doesn't have permission"
    echo ""
    echo "Next steps:"
    echo "  1. Log in to Azure Portal: https://portal.azure.com"
    echo "  2. Go to your resource: $RESOURCE_NAME"
    echo "  3. Click 'Model deployments' in the left menu"
    echo "  4. Note the exact deployment name"
    echo "  5. Use that name in your configuration"
fi

echo ""
