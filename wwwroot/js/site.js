// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


let timeout;

function resetTimeout() {
    clearTimeout(timeout);
    timeout = setTimeout(showLogoutWarning, 240000);
}

function showLogoutWarning() {
    alert("5 dakika boyunca işlem yapılmadı, oturumunuz sonlandırılacak.");
    setTimeout(() => window.location.href = '/Kullanici/Logout', 60000);
}

window.onload = resetTimeout;
document.onmousemove = resetTimeout;
document.onkeypress = resetTimeout;
