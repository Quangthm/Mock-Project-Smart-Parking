import { store } from "../../../lib/store"

export const ownerData = {
  addAuditLog: store.addAuditLog,
  createLot: store.createLot,
  createNotification: store.createNotification,
  createUser: store.createUser,
  deleteUser: store.deleteUser,
  findUserByEmail: store.findUserByEmail,
  generateSlots: store.generateSlots,
  getBookingsByLot: store.getBookingsByLot,
  getLotsByOwner: store.getLotsByOwner,
  getUsers: store.getUsers,
  notifyUser: store.notifyUser,
  saveLot: store.saveLot,
  saveUser: store.saveUser,
}
