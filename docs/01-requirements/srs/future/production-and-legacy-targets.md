# Future production targets and retained source assumptions

**FUTURE / reference only.** These original target tables do not replace the approved local acceptance profile and are not measured results. Historic budget/staffing assumptions below are not a new PM commitment.

### 4.1 Performance Requirements

| Metric | Requirement | Condition |
| :---- | :---- | :---- |
| API Response Time (p95) | \< 500ms | Normal load (100 concurrent users) |
| API Response Time (p99) | \< 1000ms | Normal load (100 concurrent users) |
| API Response Time (p95) | \< 2000ms | Peak load (1000 concurrent users) |
| Database Query Time | \< 100ms | Indexed queries |
| Database Query Time (complex) | \< 300ms | Joins, aggregations |
| Web App Initial Load | \< 3s | 3G connection, first visit |
| Parking Status Update Latency | \< 500ms | End-to-end to mobile app |

| Metric | Requirement |
| :---- | :---- |
| API Requests per Second (sustained) | 1,000 req/s |
| API Requests per Second (peak) | 2,000 req/s |
| Concurrent Users | 1,000 users |
| Concurrent Users (peak) | 5,000 users |
| Database Transactions per Second | 500 TPS |

### 4.2 Availability & Reliability

| Component | Availability Target | RTO | RPO |
| :---- | :---- | :---- | :---- |
| API Gateway | 99.9% | 5 minutes | 0 |
| Microservices | 99.9% | 5 minutes | 0 |
| Database (Primary) | 99.99% | 30 seconds | \< 5 minutes |
| Redis Cluster | 99.9% | 10 seconds | \< 1 minute |
| Message Queue | 99.9% | 30 seconds | \< 5 minutes |
| Payment Integration | 99.95% | 15 minutes | 0 |
| System Overall | 99.5% | 15 minutes | \< 5 minutes |

### 4.4 Scalability Requirements

| Metric | Requirement |
| :---- | :---- |
| Microservice Instances | Scale to 10 instances per service |
| Database Connections | Scale to 200 connections |
| Redis Cluster | Scale to 6 nodes (3 primary \+ 3 replica) |
| Message Queue | Scale to 5 nodes |
| Database Size | 1TB (Year 1), 5TB (Year 3\) |
| User Records | 100K users (Year 1), 500K users (Year 3\) |
| Transaction Records | 500K transactions/day at scale |

### 5.3 Assumptions

1. Users have smartphones running iOS or Android  
2. Users grant location permissions and have internet connectivity  
3. Parking lots have basic IoT infrastructure (parking sensors)  
4. Payment gateways are integrated and operational  
5. Parking attendants receive basic system training  
6. Stable internet connectivity is available at parking lots  
7. Users have basic smartphone literacy

### 5.4 Constraints

1. **Legal**: Must comply with Law No. 91/2025/QH15 on Personal Data Protection 
2. **Technical**: Dependent on reliable IoT sensor operation (Future/Deferred)  
3. **Financial**: Development cost estimated at \$50,000 \- \$150,000  
5. **Resources**: Requires 1 BAs, 2 BEs, 2 FEs, 1 DevOps, 1 QA, 1 PM

Supersession: GPS denial has manual fallback; hardware is fixture-only for current acceptance; payment modes follow one real sandbox plus fixtures/cash. No production budget or named staffing assignment is inferred from historical examples.
