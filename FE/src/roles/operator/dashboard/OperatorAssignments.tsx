import { useEffect, useState } from 'react';
import { operatorsApi, type OperatorAssignment } from '../../../lib/parkingApi';
import { useApp } from '../../../context/AppContext';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';

export function OperatorAssignments() {
  const { user } = useApp();
  const [assignments, setAssignments] = useState<OperatorAssignment[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let current = true; setLoading(true); setAssignments([]); setError('');
    operatorsApi.assignments().then(result => { if (current) setAssignments(result); })
      .catch(err => { if (current) setError(err instanceof Error ? err.message : 'Cannot load assignments.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, reload]);
  return <DashboardSidebar groups={[{ items: [{ id: 'assignments', label: 'My assignments', icon: '♙', active: true, onClick: () => setReload(value => value + 1) }] }]}>
    <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
      <h1>My operator assignments</h1>
      <button className="btn-outline" disabled={loading} onClick={() => setReload(value => value + 1)}>Refresh</button>
      {loading && <p role="status">Loading assignments…</p>}
      {error && <p role="alert">{error}</p>}
      {!loading && !error && !assignments.length && <p>No active parking site assignments.</p>}
      {assignments.map(assignment => <div className="card" key={assignment.siteId} style={{ marginTop: '1rem' }}>
        <strong>Site: {assignment.siteId}</strong><p>{assignment.permissions.join(', ')}</p>
      </div>)}
      <p>Device operations, cash collection and appeals will become available as those services are connected.</p>
    </main>
  </DashboardSidebar>;
}
