Role: Full-Stack Software Engineer & System Architect
Task: Implement a comprehensive SuperAdmin Role & Permission Management Dashboard and enforce granular Access Control (RBAC) across the entire application.

### 1. Database & Backend Architecture
- **Permissions Registry**: Create a database model or a central static registry that maps every system feature/endpoint, grouped logically by module/controller (e.g., `Properties`, `Units`, `Agencies`, `Users`, `Billing`).
- **Role-Permission Mapping**: Establish a many-to-many relationship between `Roles` (e.g., SuperAdmin, AgencyAdmin, Agent, Viewer) and `Permissions`.
- **API Endpoints**:
  - `GET /api/roles`: List all existing roles and their assigned permissions.
  - `POST /api/roles`: Create a new custom role.
  - `PUT /api/roles/:id`: Update permissions matrix for a specific role.
  - `DELETE /api/roles/:id`: Delete a role (prevent deletion of core system roles like SuperAdmin).
- **Backend Enforcement**: Create a middleware/guard (e.g., `requirePermission('properties:delete')`) and apply it to all relevant backend API routes.

### 2. Frontend: SuperAdmin Role Dashboard Page
- **Route & Component**: Create a new page/component at `/super-admin/roles` accessible only to SuperAdmins.
- **Role Selection/Listing**: Display a sidebar or tabbed list of all system roles. Allow creating/editing roles.
- **Modular Permission Matrix**: 
  - For the selected role, display modular "Cards" grouped by controller/feature module (e.g., "Property Management", "Unit Management").
  - Inside each card, render a clean checklist of actionable permissions (e.g., `[x] Can Add Property`, `[ ] Can Delete Property`).
  - Include "Select All / Deselect All" toggles per module card and globally.
- **State Management**: Implement optimistic UI updates with a "Save Changes" action that commits the permission matrix state to the backend.

### 3. Frontend: App-Wide Permission Enforcement
- **Permission Hook/Context**: Create a React hook or context provider (e.g., `usePermission()`) that fetches the current user's role and flattened permission list upon authentication.
- **Conditional UI Rendering**: Wrap UI components, buttons, and navigation links (e.g., "Add Property" button, "Delete" actions) with a permission gate component (`<Can permission="property:create">...</Can>`).
- **Route Guarding**: Protect frontend routes based on required permissions.
