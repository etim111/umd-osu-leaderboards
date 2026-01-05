let players = [];
let currentSortBy = 'globalRank';
let ascending = true;
let currentStatLabel = 'Global Rank';

fetch('leaderboard.json')
    .then(response => response.json())
    .then(data => {
        players = data;
        players.sort((a, b) => {
            if (a.globalRank === 0 && b.globalRank === 0) return 0;
            if (a.globalRank === 0) return 1;  // a goes after b
            if (b.globalRank === 0) return -1; // b goes after a
            return a.globalRank - b.globalRank;
        }); // Sort by rank (lowest first)
        displayPlayers(players);

        document.querySelector('.sort-arrow').addEventListener('click', () => {
            ascending = !ascending;

            const arrowButton = document.querySelector('.sort-arrow');
            arrowButton.classList.toggle('rotated');

            sortAndDisplay();
        });

        document.querySelectorAll('.filter-container button:not(.sort-arrow)').forEach(button => {
            button.addEventListener('click', (e) => {
                const buttonText = e.target.textContent;

                if (buttonText.includes('Global Rank')) {
                    currentSortBy = 'globalRank';
                    currentStatLabel = 'Global Rank';
                    ascending = true;
                } else if (buttonText.includes('Top Play')) {
                    currentSortBy = 'topPP';
                    currentStatLabel = 'Top play';
                    ascending = false;
                } else if (buttonText.includes('Top 5 Avg')) {
                    currentSortBy = 'top5AvgPP';
                    currentStatLabel = 'Top 5 avg';
                    ascending = false;
                } else if (buttonText.includes('Playtime')) {
                    currentSortBy = 'playTime';
                    currentStatLabel = 'Playtime';
                    ascending = false;
                }

                const arrowButton = document.querySelector('.sort-arrow');
                if (ascending) {
                    arrowButton.classList.remove('rotated');
                } else {
                    arrowButton.classList.add('rotated');
                }

                sortAndDisplay();
            })
        })
    })
    .catch(error => console.error('Error loading data:', error));

function displayPlayers(playerList) {
    const container = document.querySelector('.leaderboard-entries-container');

    container.innerHTML = '';

    playerList.forEach((player, index) => {
        const card = createPlayerCard(player, index + 1);
        container.appendChild(card);
    });
}

function createPlayerCard(player, placement) {
    const card = document.createElement('div');
    card.id = `profile${player.profileId}`;

    card.innerHTML = `
        <span class="placement-label">#${placement}</span>
        <a class="profile-img" target="_blank" and rel="noopener noreferrer" href="https://osu.ppy.sh/users/${player.profileId}">
            <img class="profile-img" src="https://a.ppy.sh/${player.profileId}"/>
        </a>
        <span class="profile-info">
            <span class="username">${player.username}</span>&nbsp;| pp: ${player.totalPP.toLocaleString()} | acc: ${player.hitAccuracy.toFixed(2)}%
        </span>
        <span class="sorted-stat">
            ${currentStatLabel}:&nbsp;
            <span class="stat-value">${
                currentSortBy === 'globalRank' ?
                    (player.globalRank !== 0 ? `#${player.globalRank.toLocaleString()}` : '--') :
                currentSortBy === 'topPP' ? player.topPP.toLocaleString() :
                currentSortBy === 'top5AvgPP' ? player.top5AvgPP :
                currentSortBy === 'playTime' ? player.playTime.toLocaleString() : ''
            }</span>${
                currentSortBy === 'topPP' || currentSortBy === 'top5AvgPP' ? ' pp' :
                currentSortBy === 'playTime' ? '&nbsphrs' : ''
            }
        </span>
    `;

    return card;
}

function sortAndDisplay() {
    players.sort((a, b) => {
        let valueA = a[currentSortBy];
        let valueB = b[currentSortBy];

        if (valueA === 0 && valueB === 0) return 0;
        if (valueA === 0) return 1;  // a goes after b
        if (valueB === 0) return -1; // b goes after a

        if (ascending) {
            return valueA - valueB; //lowest to highest
        } else {
            return valueB - valueA; //highest to lowest
        }
    });

    displayPlayers(players);
}
