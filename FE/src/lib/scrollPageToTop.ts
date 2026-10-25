export function scrollPageToTop() {
  window.scrollTo(0, 0)
  document.querySelectorAll<HTMLElement>("#main-content, .dashboard-content, .owner-workspace-main, .operator-workspace-main, .admin-workspace-main")
    .forEach((element) => { element.scrollTop = 0 })
}
