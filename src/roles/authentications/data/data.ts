import { store } from "../../../lib/store"

export const authData = {
  addAuditLog: store.addAuditLog,
  saveUser: store.saveUser,
  notifyUser: store.notifyUser,
  notifyRole: store.notifyRole,
  createApplication: store.createApplication,
  createUser: store.createUser,
  findUserByEmail: store.findUserByEmail,
}
