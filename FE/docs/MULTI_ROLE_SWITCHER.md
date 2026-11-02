# Account switcher: frontend state and server dependency

The profile menu for Driver, Owner, and Operator opens a circular workspace chooser. It shows the authenticated workspace as selectable. The **Add Account** card previews only allowed role relationships: Driver to Owner, Owner to Driver, and Operator to Driver. Admin has no switcher. The rounded **Sign Out** control uses the existing sign-out action.

The current authentication response and `User` model provide one role. No endpoint currently supplies all approved roles for an account, activates another role on the same account, or issues authorization for a selected role. The existing Owner sign-up flow creates a new account, so this screen does not submit to it. Preview cards never change `User.role`, tokens, or dashboard routing.

To enable switching, the server must return verified role memberships and their approval/assignment states, support any needed role activation for an existing account, and issue a role-authorized session or token when a role is selected. The FE can then display only active roles and navigate after the server confirms the switch.

Manual checks: open and close the chooser from each non-Admin profile menu; use keyboard Tab and Escape; confirm light/dark and mobile layout; preview Add Account; verify Sign Out. Role switching cannot be tested until the server contract exists.
