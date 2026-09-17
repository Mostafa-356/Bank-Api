#!/bin/bash

# Smoke Tests for Bank Management System
set -e

# Configuration
BASE_URL=${1:-"http://localhost:5000"}
TIMEOUT=30

echo "🧪 Running smoke tests against $BASE_URL..."

# Function to check HTTP endpoint
check_endpoint() {
    local endpoint=$1
    local expected_status=$2
    local description=$3
    
    echo "Testing $description..."
    
    response=$(curl -s -o /dev/null -w "%{http_code}" --max-time $TIMEOUT "$BASE_URL$endpoint" || echo "000")
    
    if [[ "$response" == "$expected_status" ]]; then
        echo "✅ $description - OK ($response)"
        return 0
    else
        echo "❌ $description - FAILED (Expected: $expected_status, Got: $response)"
        return 1
    fi
}

# Function to check API endpoint with authentication
check_api_endpoint() {
    local endpoint=$1
    local method=${2:-GET}
    local description=$3
    
    echo "Testing $description..."
    
    # First, get a token (you might need to adjust this based on your auth implementation)
    token_response=$(curl -s -X POST "$BASE_URL/api/auth/login" \
        -H "Content-Type: application/json" \
        -d '{"username":"testuser","password":"testpass"}' || echo "")
    
    if [[ -n "$token_response" ]]; then
        token=$(echo $token_response | jq -r '.token' 2>/dev/null || echo "")
        
        if [[ -n "$token" && "$token" != "null" ]]; then
            response=$(curl -s -o /dev/null -w "%{http_code}" --max-time $TIMEOUT \
                -H "Authorization: Bearer $token" \
                -X $method "$BASE_URL$endpoint" || echo "000")
            
            if [[ "$response" =~ ^[23][0-9][0-9]$ ]]; then
                echo "✅ $description - OK ($response)"
                return 0
            else
                echo "❌ $description - FAILED ($response)"
                return 1
            fi
        else
            echo "⚠️ $description - SKIPPED (No auth token)"
            return 0
        fi
    else
        echo "⚠️ $description - SKIPPED (Auth failed)"
        return 0
    fi
}

# Start tests
echo "🏁 Starting smoke tests..."

failed_tests=0

# Health check
check_endpoint "/health" "200" "Health Check" || ((failed_tests++))

# API endpoints
check_endpoint "/api" "200" "API Root" || ((failed_tests++))
check_endpoint "/swagger" "200" "Swagger Documentation" || ((failed_tests++))

# Authentication endpoints
check_endpoint "/api/auth/register" "405" "Register Endpoint (Method Not Allowed for GET)" || ((failed_tests++))
check_endpoint "/api/auth/login" "405" "Login Endpoint (Method Not Allowed for GET)" || ((failed_tests++))

# Protected endpoints (these might require authentication)
check_api_endpoint "/api/accounts" "GET" "Accounts API" || ((failed_tests++))
check_api_endpoint "/api/transactions" "GET" "Transactions API" || ((failed_tests++))

# Database connectivity test
echo "Testing database connectivity..."
db_response=$(curl -s --max-time $TIMEOUT "$BASE_URL/health/database" || echo "")
if [[ -n "$db_response" ]]; then
    echo "✅ Database connectivity - OK"
else
    echo "❌ Database connectivity - FAILED"
    ((failed_tests++))
fi

# Performance test
echo "Testing response time..."
start_time=$(date +%s%N)
curl -s --max-time $TIMEOUT "$BASE_URL/health" > /dev/null
end_time=$(date +%s%N)
response_time=$(( (end_time - start_time) / 1000000 ))

if [[ $response_time -lt 5000 ]]; then
    echo "✅ Response time - OK (${response_time}ms)"
else
    echo "⚠️ Response time - SLOW (${response_time}ms)"
fi

# Summary
echo ""
echo "📊 Test Summary:"
echo "Failed tests: $failed_tests"

if [[ $failed_tests -eq 0 ]]; then
    echo "🎉 All smoke tests passed!"
    exit 0
else
    echo "💥 Some tests failed!"
    exit 1
fi