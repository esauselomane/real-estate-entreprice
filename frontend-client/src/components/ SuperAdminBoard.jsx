import React from 'react';
import SystemAdminWorkspace from './SystemAdminWorkspace.jsx';

// Kept as a compatibility entry point; administration now always uses the
// backend-backed workspace rather than a separate in-memory board.
export default function SuperAdminBoard() {
  return <SystemAdminWorkspace />;
}
