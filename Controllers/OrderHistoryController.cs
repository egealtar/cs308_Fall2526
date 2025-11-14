using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CS308Main.Data;
using CS308Main.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CS308Main.Controllers
{
    //Rol kısıtı yok şuan için genel
    [Authorize]
    public class OrderHistoryController : Controller
    {
        private readonly IMongoDBRepository<Order> _orderRepository;
        private readonly ILogger<OrderHistoryController> _logger;

        public OrderHistoryController(
            IMongoDBRepository<Order> orderRepository,
            ILogger<OrderHistoryController> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                //TÜm siparişler çekiliyor daha sonra login olanlara göre filtre
                var allOrders = await _orderRepository.GetAllAsync();
                //TODO: kullanıcı filtremesi
                var userOrders = allOrders
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();

                return View(userOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading order history.");
                return View(Enumerable.Empty<Order>());
            }
        }
    }
}
