#!/bin/bash

# Determinism Test Script for AI Exception Analyzer
# Tests that the same input produces consistent outputs

set -e

API_URL="${API_URL:-http://localhost:5173/api/analyze-exception}"
NUM_RUNS="${NUM_RUNS:-5}"
OUTPUT_DIR="test_results"

echo "==================================="
echo "Determinism Test Script"
echo "==================================="
echo "API URL: $API_URL"
echo "Number of runs: $NUM_RUNS"
echo ""

# Create output directory
mkdir -p "$OUTPUT_DIR"
rm -f "$OUTPUT_DIR"/*.json

# Check if test payload exists
if [ ! -f "test_payload.json" ]; then
    echo "? Error: test_payload.json not found"
    exit 1
fi

echo "?? Test payload:"
cat test_payload.json | jq .
echo ""

# Run multiple tests
echo "?? Running $NUM_RUNS tests..."
for i in $(seq 1 $NUM_RUNS); do
    echo -n "  Run $i/$NUM_RUNS... "
    
    START_TIME=$(date +%s)
    
    curl -s -X POST "$API_URL" \
        -H "Content-Type: application/json" \
        -d @test_payload.json \
        > "$OUTPUT_DIR/output_$i.json"
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Check if request succeeded
    if jq -e '.status' "$OUTPUT_DIR/output_$i.json" > /dev/null 2>&1; then
        echo "? ($DURATION seconds)"
    else
        echo "? Failed"
        cat "$OUTPUT_DIR/output_$i.json"
        exit 1
    fi
    
    sleep 1
done

echo ""
echo "?? Analyzing results..."
echo ""

# Extract structured fields
echo "=== SEVERITY Analysis ==="
jq -r '.analysis.AnalysisText' "$OUTPUT_DIR"/output_*.json | grep -i "SEVERITY:" | sort | uniq -c
echo ""

echo "=== SAFE_TO_RETRY Analysis ==="
jq -r '.analysis.AnalysisText' "$OUTPUT_DIR"/output_*.json | grep -i "SAFE_TO_RETRY:" | sort | uniq -c
echo ""

echo "=== ROOT_CAUSE Analysis ==="
jq -r '.analysis.AnalysisText' "$OUTPUT_DIR"/output_*.json | grep -i "ROOT_CAUSE:" | sort | uniq -c
echo ""

# Compare first two outputs
echo "=== Detailed Comparison (Run 1 vs Run 2) ==="
if diff -u <(jq -r '.analysis.AnalysisText' "$OUTPUT_DIR/output_1.json") \
           <(jq -r '.analysis.AnalysisText' "$OUTPUT_DIR/output_2.json"); then
    echo "? Outputs are IDENTICAL"
else
    echo "??  Minor differences detected (acceptable for natural language)"
fi
echo ""

# Token usage statistics
echo "=== Token Usage Statistics ==="
jq -r '.analysis.TokensUsed' "$OUTPUT_DIR"/output_*.json | \
    awk '{sum+=$1; if(NR==1){min=max=$1}} {if($1<min){min=$1}; if($1>max){max=$1}} END {printf "  Min: %d, Max: %d, Avg: %.0f\n", min, max, sum/NR}'
echo ""

# Check consistency
SEVERITY_UNIQUE=$(jq -r '.analysis.AnalysisText' "$OUTPUT_DIR"/output_*.json | grep -i "SEVERITY:" | sort | uniq | wc -l)
RETRY_UNIQUE=$(jq -r '.analysis.AnalysisText' "$OUTPUT_DIR"/output_*.json | grep -i "SAFE_TO_RETRY:" | sort | uniq | wc -l)

echo "==================================="
echo "Test Results Summary"
echo "==================================="

if [ "$SEVERITY_UNIQUE" -eq 1 ] && [ "$RETRY_UNIQUE" -eq 1 ]; then
    echo "? PASS: All critical fields are DETERMINISTIC"
    echo "   - SEVERITY classifications: Consistent"
    echo "   - SAFE_TO_RETRY decisions: Consistent"
else
    echo "? FAIL: Inconsistent results detected"
    echo "   - SEVERITY variations: $SEVERITY_UNIQUE"
    echo "   - SAFE_TO_RETRY variations: $RETRY_UNIQUE"
    exit 1
fi

echo ""
echo "?? Detailed results saved to: $OUTPUT_DIR/"
echo ""
