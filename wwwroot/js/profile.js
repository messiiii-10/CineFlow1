document.addEventListener('DOMContentLoaded', function() {
    // Sekme tıklamaları scroll position'ı koru
    const switches = document.querySelectorAll('.profile-switcher__item');
    
    switches.forEach(item => {
        item.addEventListener('click', function(e) {
            // Scroll position'ını sakla
            const scrollPos = window.scrollY;
            sessionStorage.setItem('profileScroll', scrollPos);
        });
    });

    // Sayfa yüklendikten sonra scroll position'ını geri yükle
    const savedScroll = sessionStorage.getItem('profileScroll');
    if (savedScroll !== null) {
        window.scrollTo(0, parseInt(savedScroll));
        sessionStorage.removeItem('profileScroll');
    }
});
