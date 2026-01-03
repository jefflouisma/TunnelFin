# Performance Testing Guide (T132)

**Feature**: 003-core-integration  
**Date**: 2026-01-02  
**Purpose**: Validate remaining success criteria (SC-004, SC-007, SC-008)

---

## Overview

This guide provides instructions for manual performance testing of TunnelFin's core integration layer. These tests validate the remaining success criteria that require load testing and failure injection.

---

## Prerequisites

- ✅ Jellyfin running on Kubernetes (http://192.168.64.6:8096)
- ✅ TunnelFin plugin installed and configured
- ✅ Tribler network connectivity (6+ relay peers)
- ✅ At least one indexer configured (Torznab or HTML scraper)
- ⚠️ Load testing tools: `ab` (Apache Bench) or `wrk`
- ⚠️ Network monitoring tools: `tcpdump`, `wireshark`, or `iftop`

---

## Test 1: SC-004 - Concurrent Streams

**Success Criteria**: System handles 10 concurrent streaming sessions without degradation

### Test Procedure

1. **Prepare 10 different magnet links** (use Big Buck Bunny or other public domain content)

2. **Start 10 concurrent streams** via Jellyfin API:
   ```bash
   # Terminal 1-10 (run in parallel)
   curl -X POST "http://192.168.64.6:8096/TunnelFin/stream/start" \
     -H "Content-Type: application/json" \
     -d '{"magnetLink": "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c"}'
   ```

3. **Monitor system resources**:
   ```bash
   # CPU and memory usage
   kubectl -n jellyfin top pods
   
   # Network bandwidth
   iftop -i eth0
   ```

4. **Verify playback quality**:
   - Open 10 Jellyfin player tabs
   - Start playback on all streams
   - Monitor for buffering, stuttering, or errors
   - Check download rates remain stable

### Expected Results

- ✅ All 10 streams start successfully
- ✅ No buffering or stuttering during playback
- ✅ CPU usage < 80%
- ✅ Memory usage < 4GB
- ✅ Network bandwidth distributed evenly
- ✅ No error messages in Jellyfin logs

### Failure Criteria

- ❌ Any stream fails to start
- ❌ Buffering occurs on any stream
- ❌ CPU usage > 90%
- ❌ Memory usage > 8GB
- ❌ Error messages in logs

---

## Test 2: SC-007 - Rate Limiting

**Success Criteria**: Rate limiting prevents more than 1 request/second per indexer under load

### Test Procedure

1. **Configure a single Torznab indexer** (e.g., Jackett)

2. **Generate load with Apache Bench**:
   ```bash
   # Send 100 requests with 10 concurrent connections
   ab -n 100 -c 10 "http://192.168.64.6:8096/TunnelFin/search?query=test"
   ```

3. **Monitor indexer request rate**:
   ```bash
   # Check Jellyfin logs for rate limiting
   kubectl -n jellyfin logs -f <pod-name> | grep "Rate limit"
   
   # Check Jackett logs for request timestamps
   docker logs jackett | grep "API request" | tail -20
   ```

4. **Analyze request timestamps**:
   - Extract timestamps from logs
   - Calculate time delta between consecutive requests
   - Verify no two requests are <1 second apart

### Expected Results

- ✅ Rate limiting enforced (max 1 req/s per indexer)
- ✅ Requests queued when limit exceeded
- ✅ No indexer receives >1 req/s
- ✅ Log messages indicate rate limiting active
- ✅ Search results still returned (with delay)

### Failure Criteria

- ❌ Indexer receives >1 req/s
- ❌ Rate limiting not enforced
- ❌ Requests dropped instead of queued
- ❌ No log messages about rate limiting

---

## Test 3: SC-008 - Circuit Failover

**Success Criteria**: Circuit failover completes within 10 seconds when primary circuit fails

### Test Procedure

1. **Establish a streaming session** with circuit routing:
   ```bash
   # Start stream via TunnelFin
   curl -X POST "http://192.168.64.6:8096/TunnelFin/stream/start" \
     -H "Content-Type: application/json" \
     -d '{"magnetLink": "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c"}'
   ```

2. **Identify active circuit**:
   ```bash
   # Check TunnelFin logs for circuit ID
   kubectl -n jellyfin logs <pod-name> | grep "Circuit established"
   ```

3. **Simulate circuit failure** (choose one method):
   
   **Method A: Network partition**
   ```bash
   # Block traffic to relay peer
   sudo iptables -A OUTPUT -d <relay-peer-ip> -j DROP
   ```
   
   **Method B: Kill circuit via API** (if available)
   ```bash
   curl -X DELETE "http://192.168.64.6:8096/TunnelFin/circuit/<circuit-id>"
   ```

4. **Monitor failover time**:
   ```bash
   # Start timer when circuit fails
   # Watch logs for new circuit establishment
   kubectl -n jellyfin logs -f <pod-name> | grep -E "Circuit failed|Circuit established"
   ```

5. **Verify playback continues**:
   - Check Jellyfin player for buffering
   - Verify download rate recovers
   - Confirm no playback interruption

6. **Cleanup**:
   ```bash
   # Remove iptables rule
   sudo iptables -D OUTPUT -d <relay-peer-ip> -j DROP
   ```

### Expected Results

- ✅ Circuit failure detected within 2 seconds
- ✅ New circuit established within 10 seconds
- ✅ Playback continues without interruption
- ✅ Download rate recovers to previous level
- ✅ Log messages indicate successful failover

### Failure Criteria

- ❌ Failover takes >10 seconds
- ❌ Playback stops or buffers
- ❌ Download rate drops to zero
- ❌ No failover attempt made
- ❌ Error messages in logs

---

## Test Execution Checklist

- [ ] **SC-004**: 10 concurrent streams tested
- [ ] **SC-007**: Rate limiting validated
- [ ] **SC-008**: Circuit failover tested
- [ ] All test results documented
- [ ] Performance metrics recorded
- [ ] Issues filed for any failures
- [ ] T132 marked as complete in tasks.md

---

## Performance Metrics Template

```markdown
## SC-004: Concurrent Streams
- Streams tested: 10
- Success rate: X/10 (X%)
- Average CPU usage: X%
- Peak memory usage: XGB
- Average download rate: X MB/s
- Buffering events: X
- Status: PASS/FAIL

## SC-007: Rate Limiting
- Total requests: 100
- Concurrent connections: 10
- Requests per second (actual): X
- Rate limit enforced: YES/NO
- Queued requests: X
- Dropped requests: X
- Status: PASS/FAIL

## SC-008: Circuit Failover
- Failure detection time: Xs
- Failover completion time: Xs
- Playback interruption: YES/NO
- Download rate recovery: YES/NO
- Status: PASS/FAIL
```

---

## Notes

- These tests require manual execution due to infrastructure dependencies
- Automated performance tests can be added in future iterations
- Consider using Grafana/Prometheus for real-time monitoring
- Document any edge cases or unexpected behavior
- File GitHub issues for any failures or performance degradation

---

## Next Steps

After completing T132:
1. Document results in `performance-test-results.md`
2. Mark T132 as complete in `tasks.md`
3. Update `IMPLEMENTATION_SUMMARY.md` with final status
4. Create PR to merge `003-core-integration` to `main`

