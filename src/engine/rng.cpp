#include "rng.h"

#include "scripting/luabind.h"

using namespace thrive;

struct RNG::Implementation {
    Implementation(
        Seed seed
    ) : m_seed(seed),
        m_mt(seed)
    {
    }
    Implementation()
    {
        m_seed = m_rd();
        m_mt = std::mt19937(m_seed);
    }

    Seed m_seed;
    std::random_device m_rd;
    std::mt19937 m_mt;
};


luabind::scope
RNG::luaBindings(){
    using namespace luabind;
    return class_<RNG>("RNG")
        .def("getInt", &RNG::getInt)
        .def("getReal", &RNG::getDouble)
        .def("generateRandomSeed", &RNG::generateRandomSeed)
        .def("setSeed", &RNG::setSeed)
        .def("getSeed", &RNG::getSeed)
    ;
}

RNG::RNG()
  : m_impl(new Implementation())
{
}


RNG::RNG(
    Seed seed
) : m_impl(new Implementation(seed))
{
}


RNG::~RNG(){}


void
RNG::setSeed(
    Seed seed
) {
    m_impl->m_seed = seed;
    m_impl->m_mt.seed(seed);
}


RNG::Seed
RNG::getSeed() const {
    return m_impl->m_seed;
}


RNG::Seed
RNG::generateRandomSeed() {
    return m_impl->m_rd();
}


double
RNG::getDouble(
    double min,
    double max
) {
    std::uniform_real_distribution<double> dis(min, max);
    return dis(m_impl->m_mt);
}


int
RNG::getInt(
    int min,
    int max
) {
    std::uniform_int_distribution<int> dis(min, max);
    return dis(m_impl->m_mt);
}


std::mt19937&
RNG::mersenneTwister()
{
    return m_impl->m_mt;
}


