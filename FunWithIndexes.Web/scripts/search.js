(function ($) {
    $.fn.Search = function (options) {
        return this.each(function () {
            var self = $(this);

            self.resultsArea = self.find("#resultsArea");

            self.noResultsMessage = self.find("#NoResultsMessage");
            self.hasResultsMessage = self.find("#HasResultsMessage");
            self.resultsContainer = self.find("#SearchResultsContainer");
            self.resultsCount = self.hasResultsMessage.find(".results-count");
            self.searchResultTemplate = self.resultsContainer.find("article").clone();

            self.queryField = $("#searchResultsInput");

            self.searchTermLabel = $(".search-term");

            self.oldTerm = undefined;
            self.term = self.queryField.val();

            // Paging elements
            self.paginationWrapper = self.find('ul.pagination');
            self.pageTemplate = self.paginationWrapper.find('.page').clone();
            self.pagePrevious = self.paginationWrapper.find('.prev');
            self.pageNext = self.paginationWrapper.find('.next');

            self.pageCurrent = self.paginationWrapper.find('.page-current');
            self.pageCount = self.paginationWrapper.find('.page-count');

            // Page tracking
            self.pageSize = 5;
            self.pageNumber = 1;
            self.totalPages = 0;

            // Sets up pagination for search results.
            self.paginate = function (data) {
				/* PagedResults looks like this:
                data {
                    items,
                    pageNumber,
                    pageSize,
                    totalItems,
                    totalPages
                }
                */
                self.totalPages = data.totalPages;
                self.paginationWrapper.find('.page').remove();
                if (self.totalPages > 1) {
                    self.pagePrevious.toggle(data.pageNumber > 1);
                    self.pageNext.toggle(data.pageNumber < self.totalPages);

                    self.pageCurrent.text(self.pageNumber);
                    self.pageCount.text(self.totalPages);

                    for (i = 1; i < self.totalPages + 1; i++) {
                        var page = self.pageTemplate.clone();
                        var link = page.find('a');
                        if (i === data.pageNumber)
                            link.addClass('u-pagination-v1-5--active');
                        page.data('page', i);
                        link.text(i);

                        self.pageNext.before(page);
                    }

                    self.paginationWrapper.show(0);
                } else {
                    self.paginationWrapper.hide(0);
                }
            };

            self.previous = function (evt) {
                return self.goToPage(evt, self.pageNumber - 1);
            };

            self.next = function (evt) {
                return self.goToPage(evt, self.pageNumber + 1);
            };

            self.goToPage = function (evt, pageNumber) {
                self.pageNumber = pageNumber || $(this).data('page');
                if (self.pageNumber > self.totalPages) self.pageNumber = self.totalPages - 1;
                if (self.pageNumber < 1) self.pageNumber = 1;

                self.search();
                return false;
            };

            // filter change handler
            self.handleQuery = function (evt) {

                self.term = $(this).val();
                if (self.oldTerm !== self.term) {
                    self.oldTerm = self.term;
                    self.search();
                }
                return false;
            };

            self.search = function () {
                self.resultsArea.addClass("loading");

                $.ajax({
                    type: "GET",
                    url: "/umbraco/SiteSearch/SearchApi/GetSearchResults",
                    dataType: "json",
                    data: {
                        filter: self.term,
                        pageNumber: self.pageNumber,
                        pageSize: self.pageSize
                    },
                    success: function (data) {
                        self.searchTermLabel.text(self.term);
                        self.noResultsMessage.hide();
                        self.hasResultsMessage.hide();
                        self.paginate(data);
                        self.resultsContainer.empty();

                        if (!data.items || data.items.length === 0) {
                            self.noResultsMessage.show();
                        }
                        else {
                            // Binding search result to UI
                            for (var i = 0; i < data.items.length; i++) {
                                var result = data.items[i];
                                var resultTemplate = self.searchResultTemplate.clone();

                                _populateContent(result, resultTemplate);

                                self.resultsContainer.append(resultTemplate);
                            }
                            self.resultsCount.text(data.totalItems);
                            self.hasResultsMessage.show();
                        }
                    },
                    complete: function () {
                        self.searchTermLabel.text(self.term);
                        self.resultsArea.removeClass("loading");
                    }
                });
            };

            function _populateContent(result, template) {
                var titleElem = $(template.find("h2"));
                var linkElem = $(template.find("a"));
                var spanElem = $(template.find("span"));
                linkElem.attr("href", result.url);
                linkElem.html(result.title);
                spanElem.text(result.url);

                template.find("p").first().html(result.content);
            }

            self.queryField.on("keyup", self.handleQuery);

            // Setup pagination triggers.
            self.pagePrevious.on("click", self.previous);
            self.pageNext.on("click", self.next);

            // Bind to all future page clicks.
            self.paginationWrapper.on('click', 'li.page', self.goToPage);

            self.search();
        });
    };
}(jQuery));

$(function () {
    $('section#searchListing').Search();
});